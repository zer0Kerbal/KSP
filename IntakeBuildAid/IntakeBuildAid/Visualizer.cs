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
		private static Color _selectedIntakeColor = new Color( 0f, 0.7f, 0.7f, 1f );
		private static Color _selectedEngineIntakeColor = new Color( 0f, 0.5f, 0.5f, 1f );
		private static Color _selectedEngineColor = new Color( 1f, 0.5f, 0f, 1f );
		private static Color _selectedIntakeEngineColor = new Color( 0.8f, 0.5f, 0f, 1f );
		

		private Dictionary<Part, List<SavedMaterial>> _managedParts;
		private static Part _mouseOverPart;
		
		#region Logging

		private static void DebugLog( string msg, params object[] p )
		{
#if DEBUG
			msg = "SF: " + msg;
			print( string.Format( msg, p ) );
#endif
		}

		private static void Log( string msg, params object[] p )
		{
			msg = "SF: " + msg;
			print( string.Format( msg, p ) );
		}
		#endregion Logging

		public void Start()
		{
			_material = new Material( global::IntakeBuildAid.Resource.OutlineShaderContents );
			_managedParts = new Dictionary<Part, List<SavedMaterial>>();
			GameEvents.onPartAttach.Add( OnPartAttach );
			DebugLog( "Visualizer start" );
		}

		public void Awake()
		{
			if ( _editor == null )
			{
				_editor = EditorLogic.fetch;
			}
		}

		public void OnPartAttach( GameEvents.HostTargetAction<Part, Part> eventData )
		{
			if ( PartIsIntake( eventData.host ) || PartIsEngine( eventData.host ) )
			{
				eventData.host.AddOnMouseEnter( OnMouseEnter );
				eventData.host.AddOnMouseExit( OnMouseExit );
				DebugLog( "Added events for part: {0}", eventData.host.name );
			}
		}

		private void OnMouseExit( Part p )
		{
			_mouseOverPart = null;
			DebugLog( "MouseOverPart removed");
		}

		private void OnMouseEnter( Part p )
		{
			_mouseOverPart = p;
			DebugLog( "MouseOverPart set: {0}", p.name );
		}

		private void Update()
		{
			if ( Input.GetKey( KeyCode.LeftAlt ) || Input.GetKey( KeyCode.RightAlt ) || Input.GetKey( KeyCode.AltGr ) )
			{
				if ( _mouseOverPart != null )
				{
					// mouse is over a part
					// check if part is intake
					if ( PartIsIntake(_mouseOverPart ) )
					{
						DebugLog( "Intake found: {0}", _mouseOverPart.name );
						// set color
						ColorPart( _mouseOverPart, _selectedIntakeColor );

						Part intakeEngine = FindEngineOfIntake( _mouseOverPart );
						if ( intakeEngine != null )
						{
							ColorPart( intakeEngine, _selectedIntakeEngineColor );
						}
						return;
					}
					// check if part is engine
					else if ( PartIsEngine( _mouseOverPart ) )
					{
						DebugLog( "Engine found: {0}", _mouseOverPart.name );
						ColorPart( _mouseOverPart, _selectedEngineColor );

						List<Part> engineIntakes = FindIntakesOfEngine( _mouseOverPart );
						foreach ( Part part in engineIntakes )
						{
							ColorPart( part, _selectedEngineIntakeColor );
						}
						return;
					}
					DebugLog( "Part is no intake or engine" );
					return;
				}
				else
				{
					DebugLog( "MouseOverPart is null" );
				}
			}
			// reset highlighting
			if ( _managedParts != null && _managedParts.Count > 0 )
			{
				foreach(KeyValuePair<Part, List<SavedMaterial>> kvp in _managedParts)
				{
					List<SavedMaterial> savedMaterials = kvp.Value;
					if ( savedMaterials.Count == 0 )
						continue;

					Renderer[] renderers = kvp.Key.FindModelComponents<Renderer>();

					if ( renderers.Length > 0 )
					{
						for ( int i = 0; i < renderers.Length; ++i )
						{
							renderers[i].sharedMaterial.shader = savedMaterials[i].Shader;
							renderers[i].sharedMaterial.SetColor( "_Color", savedMaterials[i].Color );
						}
					}
				}
				_managedParts.Clear();
				DebugLog( "Resetted managed parts" );
			}
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
					DebugLog( "Part colored: {0}. Color: {1}", part.name, color.ToString() );
				}
			}
		}

		private bool PartIsIntake(Part part)
		{
			if ( part.Modules.OfType<ModuleResourceIntake>() != null
						&& part.Modules.OfType<ModuleResourceIntake>().Count() > 0 )
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		private bool PartIsEngine( Part part )
		{
			if ( ( part.Modules.OfType<ModuleEnginesFX>().Any( x => x.propellants.Any( y => y.name == "IntakeAir" ) && x.propellants.Any( y => y.name == "LiquidFuel" ) ) ) // RAPIERS use ModuleEngineFX
						|| ( part.Modules.OfType<ModuleEngines>().Any( x => x.propellants.Any( y => y.name == "IntakeAir" ) && x.propellants.Any( y => y.name == "LiquidFuel" ) ) ) ) // turbojets and basic jets use ModuleEngine
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		private Part FindEngineOfIntake( Part intakePart )
		{
			if ( !PartIsIntake( intakePart ) )
			{
				return null;
			}
			int startIndex = _editor.ship.Parts.IndexOf( intakePart );
			for ( int i = startIndex; i >= 0; i-- )
			{
				if ( PartIsEngine( _editor.ship.Parts[i] ) )
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
			if ( !PartIsEngine( enginePart ) )
			{
				return null;
			}
			List<Part> intakes = new List<Part>();
			int startIndex = _editor.ship.Parts.IndexOf( enginePart );
			for ( int i = startIndex; i >= 0; i-- )
			{
				if ( PartIsEngine( _editor.ship.Parts[i] )
					&& enginePart != _editor.ship.Parts[i] )
				{
					break; // done
				}
				else if ( PartIsIntake( _editor.ship.Parts[i] ) )
				{
					intakes.Add( _editor.ship.Parts[i] );
				}
				
				// handle loop overflow
				if ( i == 0 )
				{
					i = _editor.ship.Parts.Count - 1;
				}
				if ( i == startIndex + 1 )
				{
					break; // no intakes found
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
