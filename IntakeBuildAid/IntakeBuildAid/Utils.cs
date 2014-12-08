using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace IntakeBuildAid
{
	public class Utils
	{
		public static PartType GetPartType( Part part )
		{
			// find engines by ModuleEngines and ModuleEnginesFX module with intakeair and liquidfuel propellants
			if ( ( part.Modules.OfType<ModuleEnginesFX>().Any( x => x.propellants.Any( y => y.name == "IntakeAir" ) && x.propellants.Any( y => y.name == "LiquidFuel" ) ) ) // RAPIERS use ModuleEngineFX
						|| ( part.Modules.OfType<ModuleEngines>().Any( x => x.propellants.Any( y => y.name == "IntakeAir" ) && x.propellants.Any( y => y.name == "LiquidFuel" ) ) ) ) // turbojets and basic jets use ModuleEngine
			{
				return PartType.AirBreatherEngine;
			}
			// find intakes by resource intakeair
			else if ( part.Modules.OfType<ModuleResourceIntake>() != null
						&& part.Modules.OfType<ModuleResourceIntake>().Count() > 0 )
			{
				return PartType.Intake;
			}
			else
			{
				return PartType.SomethingElse;
			}
		}


		#region Logging

		public static void DebugLog( string msg, params object[] p )
		{
#if DEBUG
			msg = "IBA: " + msg;
			UnityEngine.Debug.Log( string.Format( msg, p ) );
			//print( string.Format( msg, p ) );
#endif
		}

		public static void Log( string msg, params object[] p )
		{
			msg = "IBA: " + msg;
			UnityEngine.Debug.Log( string.Format( msg, p ) );
			//print( string.Format( msg, p ) );
		}
		#endregion Logging

	}

	public enum PartType
	{
		SomethingElse = 0,
		Intake = 1,
		AirBreatherEngine = 2
	}
}
