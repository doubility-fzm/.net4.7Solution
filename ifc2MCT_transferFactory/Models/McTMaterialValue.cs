using System;
using System.Collections.Generic;
using System.Text;

namespace ifc2MCT_transferFactory.Models
{
    public class MctMaterialValue:MctMaterial
    {
        public double Elast { get; set; }
        public double Poisson { get; set; }
        public double Thermal { get; set; }
        public double Density { get; set; }
        public double Mass { get; set; }
        public MctMaterialValue(MctUnitSystem unitSystem)
        {
            Tunit = unitSystem.TemperUnit;
        }

        public override string ToString()
        {
            if (Data1 == null)
            {
                Data1 = string.Format($"2,{Elast},{Poisson},{Thermal},{Density},{Mass}");
            }
            return base.ToString();
        }
    }
}
