using System;
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
		private static Color _selectedIntakeColor = new Color( 0f, 0.4f, 1f, 0.8f );
		private static Color _selectedEngineIntakeColor = new Color( 0f, 0.8f, 1f, 0.8f );
		private static Color _selectedEngineColor = new Color( 1f, 0.5f, 0f, 0.8f );
		private static Color _selectedIntakeEngineColor = new Color( 1f, 0.8f, 0f, 0.8f );
		
		// used for highlighting intakes and engines
		private static Dictionary<Part, List<SavedMaterial>> _managedParts;
		private static Part _mouseOverPart;

		// used to manually select and assign intakes to engines
		private static List<Part> _manualAssignedList;

		public void Start()
		{
			_material = new Material( global::IntakeBuildAid.Resource.OutlineShaderContents );
			_managedParts = new Dictionary<Part, List<SavedMaterial>>();
			_manualAssignedList = new List<Part>();
			GameEvents.onPartAttach.Add( OnPartAttach );
			LoadSettings();
			_editor = EditorLogic.fetch;
			InitStyles(); // init onscreen messages
			Utils.DebugLog( "Visualizer started" );
		}

		private void LoadSettings()
		{
			ConfigNode config = ConfigNode.Load( KSPUtil.ApplicationRootPath + "GameData/IntakeBuildAid/settings.cfg" );
			if ( config == null )
			{
				Utils.Log( "Failed to load config." );
			}
			else
			{
				// TODO load settings
			}
		}

		public void OnPartAttach( GameEvents.HostTargetAction<Part, Part> eventData )
		{
			PartType partType = Utils.GetPartType( eventData.host );
			if ( partType == PartType.AirBreatherEngine || partType  == PartType.Intake )
			{
				eventData.host.AddOnMouseEnter( OnMouseEnter );
				eventData.host.AddOnMouseExit( OnMouseExit );
				Utils.DebugLog( "Added events for part: {0}", eventData.host.name );
			}
		}

		private void OnMouseExit( Part p )
		{
			_mouseOverPart = null;
			//Utils.DebugLog( "MouseOverPart removed");
		}

		private void OnMouseEnter( Part p )
		{
			_mouseOverPart = p;
			//Utils.DebugLog( "MouseOverPart set: {0}", p.name );
		}

		private void Update()
		{
			if ( !HighLogic.LoadedSceneIsEditor )
			{
				Destroy( this );
				return;
			}

			if ( Input.GetKey( KeyCode.LeftAlt ) || Input.GetKey( KeyCode.RightAlt ) || Input.GetKey( KeyCode.AltGr ) )
			{
				if ( _mouseOverPart != null )
				{
					// mouse is over a part
					// check if part is intake
					PartType partType = Utils.GetPartType( _mouseOverPart );
					if ( partType == PartType.Intake )
					{
						Utils.DebugLog( "Intake found: {0}", _mouseOverPart.name );
						// find engine and set color
						Part intakeEngine = FindEngineOfIntake( _mouseOverPart );
						if ( intakeEngine != null )
						{
							ColorPart( _mouseOverPart, _selectedIntakeColor );
							ColorPart( intakeEngine, _selectedIntakeEngineColor );
						}
						return;
					}
					// check if part is engine
					else if ( partType == PartType.AirBreatherEngine )
					{
						Utils.DebugLog( "Engine found: {0}", _mouseOverPart.name );
						List<Part> engineIntakes = FindIntakesOfEngine( _mouseOverPart );
						Utils.DebugLog( "Intakes found: {0}", string.Join( ", ", engineIntakes.Select( x => x.name ).ToArray() ) );
						foreach ( Part part in engineIntakes )
						{
							ColorPart( _mouseOverPart, _selectedEngineColor );
							ColorPart( part, _selectedEngineIntakeColor );
						}
						return;
					}
					//Utils.DebugLog( "Part is no intake or engine" );
					return;
				}
				else
				{
					//Utils.DebugLog( "MouseOverPart is null" );
				}
			}
			else if ( Input.GetKeyDown( KeyCode.F6 ) && _mouseOverPart != null )
			{
				// get type of part
				PartType partType = Utils.GetPartType( _mouseOverPart );
				if ( partType == PartType.Intake )
				{
					if ( !_manualAssignedList.Contains( _mouseOverPart ) )
					{
						// add part to manually assigned list
						_manualAssignedList.Add( _mouseOverPart );
						ColorPart( _mouseOverPart, _selectedIntakeColor );
						Utils.DebugLog( "Part {0} added to manual assigned list.", _mouseOverPart.name );
					}
					else
					{
						// remove part from manually assigned list
						_manualAssignedList.Remove( _mouseOverPart );
						ResetColor( _mouseOverPart );
						Utils.DebugLog( "Part {0} removed from manual assigned list.", _mouseOverPart.name );
					}
				}
				else if ( partType == PartType.AirBreatherEngine )
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
			}
			else if ( Input.GetKeyDown( KeyCode.F7 ) ) // key triggers
			{
				// order intakes desceding by intake area
				Queue<Part> intakeQueue = new Queue<Part>( _editor.ship.Parts.Where( x => x.Modules.OfType<ModuleResourceIntake>().Any() )
					.OrderByDescending( x => x.Modules.OfType<ModuleResourceIntake>().First().area ) ); // queue is easier to handle when distributing items to engines - this makes sure we can only handle a part once

				Utils.Log( "Intakes found: {0}", string.Join( ", ", intakeQueue.Select( x => x.partInfo.name + ": " + x.Modules.OfType<ModuleResourceIntake>().First().area ).ToArray() ) );

				//Log( "Intakes found by type: {0}", string.Join( ", ", intakesPerType.Select( x => x.Key + " : " + x.Value.Count ).ToArray() ) );

				List<WeightedPartList> totalPartList = new List<WeightedPartList>();
				// so far all jets have intakeair ratio of 15, so we treat jets, turbos and rapiers alike

				// TODO: handle engines grouped by type, so far its by placement order
				foreach ( Part part in _editor.ship.parts )
				{
					if ( Utils.GetPartType( part ) == PartType.AirBreatherEngine )
					{
						WeightedPartList wpl = new WeightedPartList();
						wpl.AddPart( part );
						totalPartList.Add( wpl );
					}
				}

				Utils.Log( "Jets found: {0}", string.Join( ", ", totalPartList.Select( x => x.PartList.First().partInfo.name ).ToArray() ) );

				// some sanity checks
				if ( intakeQueue.Count > 0 && totalPartList.Count > 0 )
				{
					// strip ship from intakes and jets
					_editor.ship.parts.RemoveAll( x => intakeQueue.Contains( x ) );
					Utils.DebugLog( "removed intakes temporarily" );
					_editor.ship.parts.RemoveAll( x => totalPartList.Select( y => y.PartList.First() ).Contains( x ) );
					Utils.DebugLog( "removed jets temporarily" );

					int intakeCount = intakeQueue.Count;
					for ( int i = 0; i < intakeCount; i++ )
					{
						Part part = intakeQueue.Dequeue();
						totalPartList.Where( x => x.IntakeAreaSum == totalPartList.Min( y => y.IntakeAreaSum ) ).First().AddPart( part ); // WeightedPartList with the least IntakeAreaSum will get the next intake assigned
					}

					StringBuilder sb = new StringBuilder(); // for message shown on GUI
					sb.AppendLine( "SyncFlameout assigned the following intakes to the engines:" );
					// go through all part lists, reverse them and add them back to ship
					foreach ( WeightedPartList partList in totalPartList )
					{
						partList.PartList.Reverse();
						_editor.ship.parts.AddRange( partList.PartList ); // add parts for engine and its intakes back to ship
						Utils.Log( "Intake/engine set: {0}, total intake area: {1}", string.Join( ", ", partList.PartList.Select( x => x.name ).ToArray() ), partList.IntakeAreaSum );
						sb.AppendLine( string.Format( "{0}, total intake area: {1}", string.Join( ", ", partList.PartList.Select( x => x.name ).ToArray() ), partList.IntakeAreaSum ) );
					}

					Utils.Log( "Finished intakes - jets balance" );
					OSDMessage( sb.ToString(), 3 );
				}
				else
				{
					Utils.Log( "There are either no intakes or no engines" );
					OSDMessage( "There are either no intakes or no engines", 2 );
				}
			}
			else if ( Input.GetKeyDown( KeyCode.F8 ) )
			{
				// dump some logs
				if ( _managedParts != null && _managedParts.Count > 0 )
				{
					Utils.DebugLog( "ManagedParts: {0}", string.Join(", ", _managedParts.Select( x => x.Key.name ).ToArray() ) );
				}
				if ( _manualAssignedList != null && _manualAssignedList.Count > 0 )
				{
					Utils.DebugLog( "ManualList: {0}", string.Join( ", ", _manualAssignedList.Select( x => x.name ).ToArray() ) );
				}
			}

			// reset highlighting of not manually assigned parts
			if ( _managedParts != null && _managedParts.Count > 0 )
			{
				// use tolist to create a new list - we cant modify the existing one while iterating... resetcolor also modifies this list
				foreach ( KeyValuePair<Part, List<SavedMaterial>> kvp in _managedParts.ToList() )
				{
					if ( _manualAssignedList.Contains( kvp.Key ) )
					{
						// we handle coloring of manually assigned intakes somewhere else
						continue;
					}
					ResetColor( kvp.Key );
				}
				_managedParts.Clear();
				//Utils.DebugLog( "Resetted managed parts" );
			}
		}

		public void OnDestroy()
		{
			if ( _managedParts != null && _managedParts.Count > 0 )
			{
				foreach ( KeyValuePair<Part, List<SavedMaterial>> kvp in _managedParts )
				{
					ResetColor( kvp.Key );
				}
			}
			_managedParts.Clear();
			_manualAssignedList.Clear();
			Utils.DebugLog( "OnDestroy" );
			Destroy( this );
		}

		private void ColorPart( Part part, Color color )
		{
			if ( !_managedParts.ContainsKey( part ) )
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
					Utils.DebugLog( "Part colored: {0}. Color: {1}", part.name, color.ToString() );
				}
			}
		}

		private void ResetColor( Part part )
		{
			if ( !_managedParts.ContainsKey( part ) )
			{
				return;
			}
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
			_managedParts.Remove( part );
		}

		private Part FindEngineOfIntake( Part intakePart )
		{
			if ( Utils.GetPartType( intakePart ) != PartType.Intake )
			{
				return null;
			}
			int startIndex = _editor.ship.Parts.IndexOf( intakePart );
			for ( int i = startIndex; i <= _editor.ship.Parts.Count; i++ )
			{
				if ( Utils.GetPartType( _editor.ship.Parts[i] ) == PartType.AirBreatherEngine )
				{
					return _editor.ship.Parts[i];
				}

				// handle loop overflow
				if ( i == _editor.ship.Parts.Count )
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
			if ( Utils.GetPartType( enginePart ) != PartType.AirBreatherEngine )
			{
				Utils.DebugLog("Part is no engine, cant find its intakes.");
				return null;
			}
			List<Part> intakes = new List<Part>();
			int startIndex = _editor.ship.Parts.IndexOf( enginePart ); // find index of engine in the part list
			for ( int i = startIndex - 1; i >= 0; i-- ) // iterate backwards from the engine, find all intakes
			{
				PartType partType = Utils.GetPartType( _editor.ship.Parts[i] );
				if ( partType == PartType.AirBreatherEngine )
				{
					Utils.DebugLog( "FindIntakesOfEngine at next engine: {0}", _editor.ship.Parts[i].name );
					break; // we found another engine, done
				}
				else if ( partType == PartType.Intake )
				{
					intakes.Add( _editor.ship.Parts[i] ); // add found intake to the list
				}
				
				// handle loop overflow, if there is no engine at the end of the partlist, there might be some more intakes that belong to the engine at the end of the list.
				if ( i == 0 )
				{
					i = _editor.ship.Parts.Count - 1; // start at the end of the list again
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

		#region GUI

		GUIStyle osdLabelStyle;
		private void InitStyles()
		{
			osdLabelStyle = new GUIStyle();
			osdLabelStyle.stretchWidth = true;
			osdLabelStyle.alignment = TextAnchor.MiddleCenter;
			osdLabelStyle.fontSize = 24;
			osdLabelStyle.fontStyle = FontStyle.Bold;
			osdLabelStyle.normal.textColor = Color.black;
		}

		float messageCutoff = 0;
		string messageText = "";
		private void OSDMessage( string message, float delay )
		{
			messageCutoff = Time.time + delay;
			messageText = message;
			Utils.DebugLog( messageText );
		}

		private void DisplayOSD()
		{
			if ( Time.time < messageCutoff )
			{
				GUILayout.BeginArea( new Rect( 0, ( Screen.height / 4 ), Screen.width, Screen.height / 2 ), osdLabelStyle );
				GUILayout.Label( messageText, osdLabelStyle );
				GUILayout.EndArea();
			}
		}
		public void OnGUI()
		{
			_editor = EditorLogic.fetch;
			if ( _editor == null )
				return;

			DisplayOSD();
		}

		#endregion
	}

	internal class SavedMaterial
	{
		internal Shader Shader { get; set; }
		internal Color Color { get; set; }
	}

	internal class WeightedPartList
	{
		internal float IntakeAreaSum { get; set; }
		internal List<Part> PartList { get; set; }
		internal WeightedPartList()
		{
			PartList = new List<Part>();
			IntakeAreaSum = 0;
		}

		internal void AddPart( Part part )
		{
			PartList.Add( part );
			if ( part.Modules.OfType<ModuleResourceIntake>().Any() )
			{
				IntakeAreaSum += part.Modules.OfType<ModuleResourceIntake>().First().area;
			}
		}
	}
}
