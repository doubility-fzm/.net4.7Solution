using System;
using System.Collections.Generic;
using System.Text;

namespace ifc2MCT_transferFactory.Models
{
    public class MctSectionDBUSER : MctSection
    {
        public string Data { get; set; }
        public string DbName { get; set; }
        public string SectionName { get; set; }
        public List<double> Dimensions { get; set; }
        public bool IsDb { get; set; }

        public MctSectionDBUSER()
        {
            Type = "DBUSER";
            Shape = "2L";
        }
        public MctSectionDBUSER(MctSectionDBUSERShapeTypeEnum shape,List<double> dimensions,ref int sectionIndex)
        {
            Id = sectionIndex;
            Name = "横梁";
            if (shape == MctSectionDBUSERShapeTypeEnum.DOUBLE_L)
                Shape = "2L";
            Dimensions = dimensions;
            Type = "DBUSER";
            Data = "2";
        }
        public override string ToString() 
        {
            if(Data==null)
            {
                //采用DB中的预定义的截面的时候需要输入截面的名字
                Data = $"1,{DbName},{SectionName}";
            }
            else
            {
                if(Dimensions==null)
                {
                    throw new ArgumentException("Dimensions must be provided when using User defined section");
                }
                Data = "2";
                const int CAPACITY = 10;
                for(int i=0;i<CAPACITY;i++)
                {
                    if (i < Dimensions.Count)
                        Data += $",{Dimensions[i]}";
                    else
                        Data += ",";
                }
            }
            return $"{base.ToString()},{Data}";
        }
    }
    public enum MctSectionDBUSERShapeTypeEnum
    {
        //一共有22种，这里暂时先用论文种用到的
        DOUBLE_L = 0
    }
}
