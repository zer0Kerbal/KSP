using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace IntakeBuildAid
{
	[KSPAddon( KSPAddon.Startup.EditorAny, false )]
	public class Visualizer : MonoBehaviour
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
			Utils.DebugLog( "Visualizer started" );
		}

		public void Awake()
		{
			if ( _editor == null )
			{
				_editor = EditorLogic.fetch;
			}
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
			Utils.DebugLog( "MouseOverPart removed");
		}

		private void OnMouseEnter( Part p )
		{
			_mouseOverPart = p;
			Utils.DebugLog( "MouseOverPart set: {0}", p.name );
		}

		private void Update()
		{
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
			else if ( Input.GetKey( KeyCode.F6 ) && _mouseOverPart != null )
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
			else if ( Input.GetKey( KeyCode.F8 ) )
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
				foreach ( KeyValuePair<Part, List<SavedMaterial>> kvp in _managedParts )
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
		}

		private Part FindEngineOfIntake( Part intakePart )
		{
			if ( Utils.GetPartType( intakePart ) != PartType.Intake )
			{
				return null;
			}
			int startIndex = _editor.ship.Parts.IndexOf( intakePart );
			for ( int i = startIndex; i >= 0; i-- )
			{
				if ( Utils.GetPartType( _editor.ship.Parts[i] ) == PartType.AirBreatherEngine )
				{
					return _editor.ship.Parts[i];
				}

				// handle loop overflow
				if(i == 0)
				{
					i = _editor.ship.Parts.Count - 1;
				}
				if ( i == startIndex + 1 )
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
	}

	internal class SavedMaterial
	{
		internal Shader Shader { get; set; }
		internal Color Color { get; set; }
	}
}
