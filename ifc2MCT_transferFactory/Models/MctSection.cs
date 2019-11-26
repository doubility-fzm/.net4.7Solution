using System;
using System.Collections.Generic;
using System.Text;

namespace ifc2MCT_transferFactory.Models
{
    public abstract class MctSection
    {
        public int Id { get;set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Offset { get; set; }
        public bool IsSD { get; set; }
        public bool IsWE { get; set; }
        public string Shape { get; set; }
        public MctSection()
        {
            if(Offset==null)
            {
                Offset = "CT,0,0,0,0,0,0";
            }
        }
        public override string ToString()
        {
            string bSD = IsSD ? "NO" : "YES";
            string bWE = IsWE ? "YES" : "NO";
            return string.Format($"{Id},{Type},{Name},{Offset},{bSD},{bWE},{Shape}");
        }
    }
    //public enum MctSecitionTypeEnum
    //{
    //    DBUSER = 0, COMPSITE
    //}
    //public enum MctSectionDBUSERShapeTypeEnum
    //{
    //    //一共有22种，这里暂时先用论文种用到的
    //    DOUBLE_L=0
    //}
    //public enum MctSectionCompositeShapeTypeEnum
    //{
    //    //11种暂时只用论文中用到的
    //    I=0
    //}
}
