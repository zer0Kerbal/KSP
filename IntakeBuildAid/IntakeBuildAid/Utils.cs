using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace IntakeBuildAid
{
	public class Utils
	{
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
}
