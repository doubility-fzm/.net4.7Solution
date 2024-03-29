﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace ifc2MCT_transferFactory.Models
{
    public enum MctForceUnitEnum
    {
        N=0,kN,KGF,TONF,LBF,KIPS
    }
    public enum MctLengthUnitEnum
    {
        M=0,CM,MM,FT,IN
    }
    public enum MctHeatUnitEnum
    {
        CAL=0,KCAL,J,KJ,BTU
    }
    public enum MctTemperUnitEnum
    {
        C=0,F
    }
    public class MctUnitSystem
    {
        public MctForceUnitEnum ForceUnit { get; set; }
        public MctLengthUnitEnum LengthUnit { get; set; }
        public MctHeatUnitEnum HeatUnit { get; set; }
        public MctTemperUnitEnum TemperUnit { get; set; }
        public override string ToString()
        {
            return "*UNIT\t; Unit System\n; FORCE, LENGTH, HEAT, TEMPER\n" +
                $"{ForceUnit},{LengthUnit},{HeatUnit},{TemperUnit}";
        }
        public MctUnitSystem()
        {
            ForceUnit = MctForceUnitEnum.kN;
            LengthUnit = MctLengthUnitEnum.M;
            HeatUnit = MctHeatUnitEnum.KJ;
            TemperUnit = MctTemperUnitEnum.C;
        }
    }
}
