using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace IntakeBuildAid
{
	public class SavedMaterial
	{
		public Shader Shader { get; set; }
		public Color Color { get; set; }
	}

	public class WeightedPartList
	{
		public double IntakeAreaSum { get; set; }
		public List<Part> PartList { get; set; }
		public WeightedPartList()
		{
			PartList = new List<Part>();
			IntakeAreaSum = 0;
		}

		public void AddPart( Part part )
		{
			PartList.Add( part );
			if ( part.Modules.OfType<ModuleResourceIntake>().Any() )
			{
				IntakeAreaSum += part.Modules.OfType<ModuleResourceIntake>().First().area;
			}
		}
	}

	public enum PartType
	{
		SomethingElse = 0,
		Intake = 1,
		AirBreatherEngine = 2,
		IntakeAndEngine = 3
	}

	public class ResouceSet
	{
		public string Name { get; set; }
		public string AirResourceName { get; set; }

		public ResouceSet()
		{
		}
	}
}
