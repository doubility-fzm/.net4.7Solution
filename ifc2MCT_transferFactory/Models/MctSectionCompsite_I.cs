using System;
using System.Collections.Generic;
using System.Text;

namespace ifc2MCT_transferFactory.Models
{
    public class MctSectionCompsite_I:MctSection
    {
        public List<double> SectionDimensions;
        public (double Bc, double Tc, double Hh) Plate_Info;
        public (double EsEc, double DsDc, double Ps, double Pc, double TsTc) Material_info;
        public string Data;
        public int[] Shape_NUM;
        //不考虑加劲肋的情况
        public string Stiff_NUM = $"0\n";
        public String Stiff_Shape = $"0\n";
        public string Stiff_POS = $"0\n";
        public bool IsMulti;
        public (double Elong, double Esh) Multi_Ratio;
        public MctSectionCompsite_I(List<double> sectionDimensions, (double Bc, double Tc, double Hh) deckInfo)
        {
            Shape_NUM = new int[6];
            Type = "COMPSITE";
            Shape = "I";
            SectionDimensions = sectionDimensions;
            Material_info = (6.48737, 3.26186, 0.3, 0.2, 1.2);
            //plate_Info暂时先定一个固定数据
            Plate_Info = deckInfo;
        }
        public override string ToString()
        {
            for(int i=0;i<SectionDimensions.Count;i++)
            {
                if(i==SectionDimensions.Count-1)
                {
                    Data += $"{ SectionDimensions[i]}\n";
                }
                else
                {
                    Data += $"{ SectionDimensions[i]},";
                }               
            }
            Data += $"{Shape_NUM[0]},{Shape_NUM[1]},{Shape_NUM[2]},{Shape_NUM[3]},{Shape_NUM[4]},{Shape_NUM[5]}\n";
            Data += Stiff_NUM;
            Data += Stiff_Shape;
            Data += Stiff_POS;
            Data += $"{Plate_Info.Bc},1,{Plate_Info.Bc},{Plate_Info.Bc},{Plate_Info.Tc},{Plate_Info.Hh},";
            Data += $"{Material_info.EsEc},{Material_info.DsDc},{Material_info.Ps},{Material_info.Pc},{Material_info.TsTc},";
            if (IsMulti == true)
                Data += $"YES,{Multi_Ratio.Elong},{Multi_Ratio.Esh}";
            else
                Data += $"NO, ,";
            return $"{base.ToString()}\n{Data}";
        }
    }
}
