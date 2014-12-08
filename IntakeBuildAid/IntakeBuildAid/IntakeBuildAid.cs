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


		public void Awake()
		{
			Utils.DebugLog( "IntakeBuildAid awake" );
			_editor = EditorLogic.fetch;
			InitStyles(); // init onscreen messages
		}

		public void Update()
		{
			if ( _editor == null || _editor.editorScreen != EditorLogic.EditorScreen.Parts )
			{
				return;
			}

			// bool altPressed = Input.GetKey( KeyCode.LeftAlt ) || Input.GetKey( KeyCode.RightAlt ) || Input.GetKey( KeyCode.AltGr );

			if ( Input.GetKeyDown( KeyCode.F7 ) ) // key triggers
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
					if ( Utils.GetPartType ( part ) == PartType.AirBreatherEngine )
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
					sb.AppendLine("SyncFlameout assigned the following intakes to the engines:");
					// go through all part lists, reverse them and add them back to ship
					foreach ( WeightedPartList partList in totalPartList )
					{
						partList.PartList.Reverse();
						_editor.ship.parts.AddRange( partList.PartList ); // add parts for engine and its intakes back to ship
						Utils.Log( "Intake/engine set: {0}, total intake area: {1}", string.Join( ", ", partList.PartList.Select( x => x.name ).ToArray() ), partList.IntakeAreaSum );
						sb.AppendLine( string.Format( "{0}, total intake area: {1}", string.Join( ", ", partList.PartList.Select( x => x.name ).ToArray() ), partList.IntakeAreaSum ) );
					}

					Utils.Log( "Finished intakes - jets balance" );
					OSDMessage( sb.ToString(), intakeCount );
				}
				else
				{
					Utils.Log("There are either no intakes or no engines");
					OSDMessage( "There are either no intakes or no engines", 2 );
				}
			}
			
			// this was used for some test data dumps
			//if ( altPressed && Input.GetKeyDown( KeyCode.L ) ) // alt-L dump intake parts info to log
			//{
			//	List<Part> intakeList = new List<Part>();
			//	intakeList = _editor.ship.Parts.Where( x => x.Modules.OfType<ModuleResourceIntake>().Any() ).ToList();
			//	foreach(Part part in intakeList)
			//	{
			//		Log("Intake: {0}, mass: {1}, area: {2}, area/mass: {3}",
			//			part.name, part.mass, part.Modules.OfType<ModuleResourceIntake>().First().area, part.Modules.OfType<ModuleResourceIntake>().First().area / part.mass );
			//	}
			//}
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

	internal class WeightedPartList
	{
		internal float IntakeAreaSum {get; set;}
		internal List<Part> PartList { get; set; }
		internal WeightedPartList()
		{
			PartList = new List<Part>();
			IntakeAreaSum = 0;
		}

		internal void AddPart(Part part)
		{
			PartList.Add( part );
			if ( part.Modules.OfType<ModuleResourceIntake>().Any() )
			{
				IntakeAreaSum += part.Modules.OfType<ModuleResourceIntake>().First().area;
			}
		}
	}
}
