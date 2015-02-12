using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace IntakeBuildAid
{
	[KSPAddon( KSPAddon.Startup.EditorAny, false )]
	public class IntakeBuildAid : MonoBehaviour
	{
		private EditorLogic _editor;
		private static Material _material; // shader to highlight stuff
		// highlight colors
		private static Color _selectedIntakeColor = new Color( 0f, 0.2f, 1f, 1f );
		private static Color _selectedEngineIntakeColor = new Color( 0f, 0.2f, 1f, 1f ); // todo: pick better colors...
		private static Color _selectedEngineColor = new Color( 1f, 0f, 0f, 1f );
		private static Color _selectedIntakeEngineColor = new Color( 1f, 0f, 0f, 1f ); // todo: pick better colors...
		
		// used for highlighting intakes and engines
		private static Dictionary<Part, List<SavedMaterial>> _managedParts;
		private static Part _mouseOverPart;

		// used to manually select and assign intakes to engines
		private static List<Part> _manualAssignedList;
		
		// from settings
		private static bool _useCustomShader;
		private static KeyCode _keyHighlight = KeyCode.F6;
		private static KeyCode _keyBalanceIntakes = KeyCode.F7;
		private static KeyCode _keyManualAssign = KeyCode.F8;

		// toolbar
		private ApplicationLauncherButton _launcherButton = null; // toolbar

		private bool _guiVisible;
		private Rect _guiRect;

		public void Start()
		{
			_material = new Material( global::IntakeBuildAid.Resource.OutlineShaderContents );
			_managedParts = new Dictionary<Part, List<SavedMaterial>>();
			_manualAssignedList = new List<Part>();
			GameEvents.onPartAttach.Add( OnPartAttach );
			_useCustomShader = false;
			LoadSettings();
			_editor = EditorLogic.fetch;

			// init GUI
			GameEvents.onGUIApplicationLauncherReady.Add( OnGUIApplicationLauncherReady );
			_guiRect = new Rect( ( Screen.width ) / 4, Screen.height / 2, 300, 100 );
			_guiVisible = false;
			Utils.DebugLog( "IntakeBuildAid Start() finished" );
		}

		private void LoadSettings()
		{

			ConfigNode config = ConfigNode.Load( KSPUtil.ApplicationRootPath + "GameData/IntakeBuildAid/settings.cfg" );
			
			if ( config == null )
			{
				Utils.Log( "Failed to load settings.cfg" );
			}
			else
			{
				ConfigNode rootNode = config.GetNode( "IntakeBuildAid" );
				if ( rootNode != null )
				{
					_useCustomShader = rootNode.GetValue( "useCustomShader" ) == "true";
					_keyHighlight = (KeyCode)Enum.Parse( typeof( KeyCode ), rootNode.GetValue( "keyHighlight" ) );
					_keyBalanceIntakes = (KeyCode)Enum.Parse( typeof( KeyCode ), rootNode.GetValue( "keyBalanceIntakes" ) );
					_keyManualAssign = (KeyCode)Enum.Parse( typeof( KeyCode ), rootNode.GetValue( "keyManualAssign" ) );
				}
			}
		}

		public void OnPartAttach( GameEvents.HostTargetAction<Part, Part> eventData )
		{
			PartType partType = GetPartType( eventData.host );
			if ( partType == PartType.AirBreatherEngine || partType  == PartType.Intake || partType == PartType.IntakeAndEngine )
			{
				eventData.host.AddOnMouseEnter( OnMouseEnter );
				eventData.host.AddOnMouseExit( OnMouseExit );
				Utils.DebugLog( "Added events for part: {0}", eventData.host.name );
			}
		}

		private void OnMouseExit( Part p )
		{
			_mouseOverPart = null;
		}

		private void OnMouseEnter( Part p )
		{
			_mouseOverPart = p;
		}

		private void Update()
		{
			if ( !HighLogic.LoadedSceneIsEditor )
			{
				Destroy( this );
				return;
			}

			if ( Input.GetKeyDown( _keyHighlight ) )
			{
				#region Highlight intakes and engines
				if ( _mouseOverPart != null )
				{
					ResetAllColors();
					// mouse is over a part
					// check if part is intake
					PartType partType = this.GetPartType( _mouseOverPart );
					if ( partType == PartType.Intake )
					{
						Utils.DebugLog( "Intake found: {0}", _mouseOverPart.name );
						ColorPart( _mouseOverPart, _selectedIntakeColor );
						// find engine and set color
						Part intakeEngine = FindEngineOfIntake( _mouseOverPart );
						if ( intakeEngine != null )
						{
							
							ColorPart( intakeEngine, _selectedIntakeEngineColor );
						}
						return;
					}
					// check if part is engine
					else if ( partType == PartType.AirBreatherEngine || partType == PartType.IntakeAndEngine )
					{
						Utils.DebugLog( "Engine found: {0}", _mouseOverPart.name );
						ColorPart( _mouseOverPart, _selectedEngineColor );
						List<Part> engineIntakes = FindIntakesOfEngine( _mouseOverPart );
						Utils.DebugLog( "Intakes found: {0}", string.Join( ", ", engineIntakes.Select( x => x.name ).ToArray() ) );
						foreach ( Part part in engineIntakes )
						{
							ColorPart( part, _selectedEngineIntakeColor );
						}
						return;
					}
					ResetAllColors();
					return;
				}
				else
				{
					ResetAllColors();
					
				}
				#endregion Highlight intakes and engines
			}
			else if ( Input.GetKeyDown( _keyManualAssign ) && _mouseOverPart != null )
			{
				#region Manual assign intakes to engines
				// get type of part
				PartType partType = GetPartType( _mouseOverPart );
				if ( partType == PartType.Intake )
				{
					if ( !_manualAssignedList.Contains( _mouseOverPart ) )
					{
						// add part to manually assigned list
						_manualAssignedList.Add( _mouseOverPart );
						ColorPart( _mouseOverPart, _selectedIntakeColor );
						Utils.Log( "Part {0} added to manual assigned list.", _mouseOverPart.name );
					}
					else
					{
						// remove part from manually assigned list
						_manualAssignedList.Remove( _mouseOverPart );
						ResetColor( _mouseOverPart );
						Utils.Log( "Part {0} removed from manual assigned list.", _mouseOverPart.name );
					}
				}
				else if ( partType == PartType.AirBreatherEngine || partType == PartType.IntakeAndEngine )
				{
					// end manual assignment once an engine is selected
					// add engine to manual list
					_manualAssignedList.Add( _mouseOverPart );

					// now turn off all coloring
					foreach ( Part part in _manualAssignedList )
					{
						ResetColor( part );
					}
					// remove these parts from the ship
					_editor.ship.Parts.RemoveAll( x => _manualAssignedList.Contains( x ) );
					// re-add parts to ship, these now are in a proper order
					_editor.ship.Parts.AddRange( _manualAssignedList );
					Utils.Log( "Finished manual intake-engine assignment: {0}", string.Join( ", ", _manualAssignedList.Select( x => x.name ).ToArray() ) );
					_manualAssignedList.Clear(); // reset list
				}
				#endregion Manual assign intakes to engines
			}
			else if ( Input.GetKeyDown( _keyBalanceIntakes ) ) // key triggers
			{
				#region Balance intakes to engines
				// order intakes desceding by intake area
				BalanceIntakes();
				#endregion Balance intakes to engines
			}
			#region Debug stuff

#if DEBUG
			else if ( Input.GetKeyDown( KeyCode.F11 ) )
			{
				Utils.DebugLog( "Ship partList:\r\n  {0}", string.Join( "\r\n  ", _editor.ship.parts.Select( x => x.name ).ToArray() ) );

				// dump some logs
				if ( _managedParts != null && _managedParts.Count > 0 )
				{
					Utils.DebugLog( "ManagedParts: {0}", string.Join( ", ", _managedParts.Select( x => x.Key.name ).ToArray() ) );
				}
				if ( _manualAssignedList != null && _manualAssignedList.Count > 0 )
				{
					Utils.DebugLog( "ManualList: {0}", string.Join( ", ", _manualAssignedList.Select( x => x.name ).ToArray() ) );
				}
			}
#endif
			#endregion Debug stuff
		}

		public void OnDestroy()
		{
			Utils.DebugLog( "OnDestroy" );
			if ( _managedParts != null )
			{
				foreach ( KeyValuePair<Part, List<SavedMaterial>> kvp in _managedParts )
				{
					ResetColor( kvp.Key );
				}
			}
			_managedParts.Clear();
			_manualAssignedList.Clear();

			ApplicationLauncher.Instance.RemoveModApplication( _launcherButton );
			GameEvents.onGUIApplicationLauncherReady.Remove( OnGUIApplicationLauncherReady );
			Destroy( this );
		}

		#region Helpers

		private void BalanceIntakes()
		{
			Queue<Part> intakeQueue = new Queue<Part>( _editor.ship.Parts.Where( x => GetPartType( x ) == PartType.Intake ) // do not treat intakeandengine parts as intake but as engine
				.OrderByDescending( x => x.Modules.OfType<ModuleResourceIntake>().First().area ) ); // queue is easier to handle when distributing items to engines - this makes sure we can only handle a part once

			Utils.Log( "Intakes found: {0}", string.Join( ", ", intakeQueue.Select( x => x.partInfo.title + ": " + x.Modules.OfType<ModuleResourceIntake>().First().area ).ToArray() ) );
			List<WeightedPartList> totalPartList = new List<WeightedPartList>();
			// so far all jets have intakeair ratio of 15, so we treat jets, turbos and rapiers alike
			// TODO for future: take intakeair ratio into account. how exactly? I donno :)

			// handle engines grouped by type, so far its by placement order
			foreach ( Part part in _editor.ship.parts )
			{
				if ( GetPartType( part ) == PartType.AirBreatherEngine )
				{
					WeightedPartList wpl = new WeightedPartList();
					wpl.AddPart( part );
					totalPartList.Add( wpl );
				}
				else if ( GetPartType( part ) == PartType.IntakeAndEngine )
				{
					WeightedPartList wpl = new WeightedPartList();
					wpl.IntakeAreaSum = part.Modules.OfType<ModuleResourceIntake>().First().area; // add intake area of part that has both intake and engine in one
					wpl.AddPart( part );
					totalPartList.Add( wpl );
				}
			}

			Utils.Log( "Jets found: {0}", string.Join( ", ", totalPartList.Select( x => x.PartList.First().partInfo.title ).ToArray() ) );

			if ( intakeQueue.Count > 0 && totalPartList.Count > 0 )
			{
				// strip ship from intakes and jets
				_editor.ship.parts.RemoveAll( x => intakeQueue.Contains( x ) );
				Utils.Log( "removed intakes temporarily" );
				_editor.ship.parts.RemoveAll( x => totalPartList.Select( y => y.PartList.First() ).Contains( x ) );
				Utils.Log( "removed jets temporarily" );

				int intakeCount = intakeQueue.Count;
				for ( int i = 0; i < intakeCount; i++ )
				{
					Part part = intakeQueue.Dequeue();
					totalPartList.Where( x => x.IntakeAreaSum == totalPartList.Min( y => y.IntakeAreaSum ) ).First().AddPart( part ); // WeightedPartList with the least IntakeAreaSum will get the next intake assigned
				}

				// go through all part lists, reverse them and add them back to ship
				foreach ( WeightedPartList partList in totalPartList )
				{
					partList.PartList.Reverse();
					_editor.ship.parts.AddRange( partList.PartList ); // add parts for engine and its intakes back to ship
					Utils.Log( "Intake/engine set: {0}, total intake area: {1}", string.Join( ", ", partList.PartList.Select( x => x.name ).ToArray() ), partList.IntakeAreaSum );
				}

				Utils.Log( "Finished intakes - jets balance" );
			}
			else
			{
				Utils.Log( "There are either no intakes or no engines" );
			}
		}

		private void ColorPart( Part part, Color color )
		{
			if ( !_managedParts.ContainsKey( part ) )
			{
				if ( _useCustomShader )
				{
					List<SavedMaterial> savedMaterials = new List<SavedMaterial>();
					Renderer[] renderers = part.FindModelComponents<Renderer>();

					if ( renderers.Length > 0 )
					{
						for ( int i = 0; i < renderers.Length; ++i )
						{
							savedMaterials.Insert( i, new SavedMaterial() { Shader = renderers[i].sharedMaterial.shader, Color = renderers[i].sharedMaterial.GetColor( "_Color" ) } );
							renderers[i].sharedMaterial.shader = _material.shader;
							renderers[i].sharedMaterial.SetColor( "_Color", color );
						}

						_managedParts.Add( part, savedMaterials );
					}
				}
				else
				{
					part.SetHighlight( true, false );
					part.SetHighlightColor( color );
					part.SetHighlightType( Part.HighlightType.AlwaysOn );
				}
			}
		}

		private void ResetColor( Part part )
		{
			if ( _useCustomShader )
			{
				if ( _managedParts.ContainsKey( part ) )
				{
					List<SavedMaterial> savedMaterials = _managedParts[part];
					if ( savedMaterials.Count == 0 )
					{
						_managedParts.Remove( part );
						return;
					}

					Renderer[] renderers = part.FindModelComponents<Renderer>();

					if ( renderers.Length > 0 )
					{
						for ( int i = 0; i < renderers.Length; ++i )
						{
							renderers[i].sharedMaterial.shader = savedMaterials[i].Shader;
							renderers[i].sharedMaterial.SetColor( "_Color", savedMaterials[i].Color );
						}
					}
				}
			}
			else
			{
				part.SetHighlight( false, true );
				if ( _managedParts.ContainsKey( part ) )
				{
					_managedParts.Remove( part );
				}
			}
		}

		private void ResetAllColors()
		{
			foreach ( Part part in _editor.ship.parts )
			{
				ResetColor( part );
			}
			_managedParts.Clear();
		}

		private void OnGUIApplicationLauncherReady()
		{
			// Create the button in the KSP AppLauncher
			Utils.DebugLog( "Adding toolbar icon" );
			_launcherButton = ApplicationLauncher.Instance.AddModApplication( ToggleGUI, ToggleGUI,
			null, null,
			null, null,
			ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB,
			GameDatabase.Instance.GetTexture( "IntakeBuildAid/icons/Toolbar", false ) );
		}

		private Part FindEngineOfIntake( Part intakePart )
		{
			if ( GetPartType( intakePart ) != PartType.Intake )
			{
				return null;
			}
			int startIndex = _editor.ship.Parts.IndexOf( intakePart );
			for ( int i = startIndex; i <= _editor.ship.Parts.Count - 1; i++ )
			{
				if ( GetPartType( _editor.ship.Parts[i] ) == PartType.AirBreatherEngine || GetPartType( _editor.ship.Parts[i] ) == PartType.IntakeAndEngine )
				{
					return _editor.ship.Parts[i];
				}

				// handle loop overflow
				if ( i == _editor.ship.Parts.Count - 1 )
				{
					i = 0;
				}
				if ( i == startIndex - 1 )
				{
					break; // no engines found
				}
			}
			return null;
		}

		private List<Part> FindIntakesOfEngine( Part enginePart )
		{
			PartType enginePartType = GetPartType( enginePart );
			if ( !(enginePartType == PartType.AirBreatherEngine || enginePartType == PartType.IntakeAndEngine ) )
			{
				Utils.DebugLog("Part is no engine, cant find its intakes.");
				return null;
			}
			List<Part> intakes = new List<Part>();
			int startIndex = _editor.ship.Parts.IndexOf( enginePart ); // find index of engine in the part list
			for ( int i = startIndex - 1; i >= 0; i-- ) // iterate backwards from the engine, find all intakes
			{
				PartType partType = GetPartType( _editor.ship.Parts[i] );
				if ( partType == PartType.AirBreatherEngine || partType == PartType.IntakeAndEngine )
				{
					Utils.DebugLog( "FindIntakesOfEngine at {0}, engine: {1}", i, _editor.ship.Parts[i].name );
					break; // we found another engine, done
				}
				else if ( partType == PartType.Intake )
				{
					intakes.Add( _editor.ship.Parts[i] ); // add found intake to the list
				}
				
				// handle loop overflow, if there is no engine at the end of the partlist, there might be some more intakes that belong to the engine at the end of the list.
				if ( i == 0 )
				{
					i = _editor.ship.Parts.Count; // start at the end of the list again
					Utils.DebugLog( "FindIntakesOfEngine reset loop" );
				}
				if ( i == startIndex )
				{
					Utils.DebugLog( "FindIntakesOfEngine done" );
					break; // we are through the list, abort
				}
			}
			return intakes;
		}

		private PartType GetPartType( Part part )
		{
			// find engines by ModuleEngines and ModuleEnginesFX module with intakeair propellant
			// this is for modded engines that are both intakes and engines in one part
			if ( ( ( part.Modules.OfType<ModuleEnginesFX>().Any( x => x.propellants.Any( y => y.name == "IntakeAir" ) ) )
						|| ( part.Modules.OfType<ModuleEngines>().Any( x => x.propellants.Any( y => y.name == "IntakeAir" ) ) ) )
				&& ( part.Modules.OfType<ModuleResourceIntake>().Any( x => x.resourceName == "IntakeAir" ) )
				)
			{
				return PartType.IntakeAndEngine;
			}
			// find engines by ModuleEngines and ModuleEnginesFX module with intakeair propellant
			else if ( ( part.Modules.OfType<ModuleEnginesFX>().Any( x => x.propellants.Any( y => y.name == "IntakeAir" ) ) )
						|| ( part.Modules.OfType<ModuleEngines>().Any( x => x.propellants.Any( y => y.name == "IntakeAir" ) ) )
				)
			{
				return PartType.AirBreatherEngine;
			}
			// find intakes by resource intakeair
			else if ( part.Modules.OfType<ModuleResourceIntake>().Any( x => x.resourceName == "IntakeAir" )
				)
			{
				return PartType.Intake;
			}
			else
			{
				return PartType.SomethingElse;
			}
		}
		
		#endregion Helpers

		#region GUI
		
		// old shabby onscreen message...

		//GUIStyle osdLabelStyle;
		//private void InitStyles()
		//{
		//	osdLabelStyle = new GUIStyle();
		//	osdLabelStyle.stretchWidth = true;
		//	osdLabelStyle.alignment = TextAnchor.MiddleCenter;
		//	osdLabelStyle.fontSize = 24;
		//	osdLabelStyle.fontStyle = FontStyle.Bold;
		//	osdLabelStyle.normal.textColor = Color.black;
		//}

		//float messageCutoff = 0;
		//string messageText = "";
		//private void OSDMessage( string message, float delay )
		//{
		//	messageCutoff = Time.time + delay;
		//	messageText = message;
		//	Utils.DebugLog( messageText );
		//}

		//private void DisplayOSD()
		//{
		//	if ( Time.time < messageCutoff )
		//	{
		//		GUILayout.BeginArea( new Rect( 0, ( Screen.height / 4 ), Screen.width, Screen.height / 2 ), osdLabelStyle );
		//		GUILayout.Label( messageText, osdLabelStyle );
		//		GUILayout.EndArea();
		//	}
		//}

		public void OnGUI()
		{
			if(_guiVisible)
			{
				_guiRect = GUILayout.Window( 0, _guiRect, PaintWindow, "Intake Build Aid" );
			}
		}

		private void ToggleGUI()
		{
			if ( _guiVisible )
			{
				// hide GUI

				_guiVisible = false;
				Utils.DebugLog( "GUI hidden" );
			}
			else
			{
				// show GUI
				_guiVisible = true;
				Utils.DebugLog( "GUI shown" );
			}
		}

		private void PaintWindow(int windowID)
		{
			GUILayout.BeginVertical();
			GUILayout.BeginHorizontal();
			if ( GUILayout.Button( "Autoarrange engines and intakes" ) )
			{
				BalanceIntakes();
			}
			if ( GUILayout.Button( "Close" ) )
			{
				_guiVisible = false;
			}
			GUILayout.EndHorizontal();
			foreach ( Part engine in _editor.ship.parts.Where( x => GetPartType( x ) == PartType.AirBreatherEngine ) )
			{
				List<Part> intakes = FindIntakesOfEngine( engine );
				GUILayout.Label( string.Format( "{0}: ∑={1}", engine.partInfo.title, intakes.Sum( x => x.Modules.OfType<ModuleResourceIntake>().First().area ) ) );
				foreach ( Part intake in intakes )
				{
					GUILayout.BeginHorizontal();
					GUILayout.Label( string.Format( "    {0}: {1}", intake.partInfo.title, intake.Modules.OfType<ModuleResourceIntake>().First().area ) );
					GUILayout.EndHorizontal();
				}
			}

			foreach ( Part engine in _editor.ship.parts.Where( x => GetPartType( x ) == PartType.IntakeAndEngine ) )
			{
				List<Part> intakes = FindIntakesOfEngine( engine );
				GUILayout.Label( string.Format( "{0}: ∑={1}", engine.partInfo.title, intakes.Sum( x => x.Modules.OfType<ModuleResourceIntake>().First().area ) + engine.Modules.OfType<ModuleResourceIntake>().First().area ) );
				foreach ( Part intake in intakes )
				{
					GUILayout.BeginHorizontal();
					GUILayout.Label( string.Format( "    {0}: {1}", intake.partInfo.title, intake.Modules.OfType<ModuleResourceIntake>().First().area ) );
					GUILayout.EndHorizontal();
				}
			}		
			
			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		#endregion GUI
	}
}
