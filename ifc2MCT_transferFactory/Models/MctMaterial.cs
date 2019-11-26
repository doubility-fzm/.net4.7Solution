using System;
using System.Collections.Generic;
using System.Text;

namespace ifc2MCT_transferFactory.Models
{
    public enum MctMaterialTypeEnum
    {
        STEEL = 0, CONC, USER, SRC
    }
    public abstract class MctMaterial
    {
        //参考midas材料截面的材料属性的窗口
        public int Id { get; set; }
        public MctMaterialTypeEnum Type { get; set; }
        public string Name { get; set; }
        //材料定义中比热和热传导系数
        public double Spheat { get; set; }
        public double Heatco { get; set; }
        public string Plast = " ";
        public MctTemperUnitEnum Tunit { get; set; }

        public bool UseMass { get; set; }

        public double DampRatio { get; set; }
        public string Data1 { get; set; }
        public string Data2 { get; set; }

        public MctMaterial()
        {
            if (DampRatio == 0)
                DampRatio = Type == MctMaterialTypeEnum.STEEL ? 0.02 : (Type == MctMaterialTypeEnum.USER ? 0 : 0.05);
        }

        public override string ToString()
        {
            string bMASS = UseMass ? "YES" : "NO";
            return string.Format($"{Id},{Type},{Name},{Spheat},{Heatco},{Plast},{Tunit},{bMASS},{DampRatio},{Data1}");
        }
    }
}
