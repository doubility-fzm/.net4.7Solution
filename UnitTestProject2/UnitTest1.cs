using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StructureDesignModule;
using XBIM_Module;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.ProductExtension;
using ifc2MCT;

namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var t1 = new Technical_Demand();
            t1.Write_Info2TXT();
            var t2 = new CrossSection();
            t2.calculate_girder_parament(ref t1);
            t2.calculate_stiffener_info(t2.girder_web_height,t2.girder_web_thickness);
            t2.Write_Info2TXT();
            var t3 = new longitudinal_lateral_connection();
            t3.get_parameters_truss(ref t1, ref t3, ref t2);
            t3.Write_Info2TXT();
        }
        [TestMethod]
        public void TestMethod2()
        {
            const string PATH = "../../TestFiles/alignment.ifc";
            var align = new Create_Alignment(PATH) { IsStraight = true };
            align.Create();
        }

        [TestMethod]
        public void TestMethod3()
        {
            const string PATH = "../../TestFiles/alignment-arc.ifc";
            var align = new Create_Alignment(PATH) { IsStraight = false };
            align.Create();
        }

        [TestMethod]
        public void BuildBridge_Construction_Test()
        {
            const string INPATH = "../../TestFiles/alignment.ifc";
            const string OUTPATH = "../../TestFiles/aligment&construction.ifc";
            //generate the parameter of offset distance

            var t1 = new Technical_Demand();
            var t2 = new CrossSection();
            t2.calculate_girder_parament(ref t1);
            var t3 = new longitudinal_lateral_connection();
            t3.get_parameters(ref t1, ref t3, ref t2);
            var para = new Technical_Demand();
            var cross = new CrossSection();
            cross.calculate_girder_parament(ref para);
            const int START = 30000, END = 60000;
            double startGap = 40;
            double endGap = 40;
            var thickness = cross.GetThickness();

            var sectionParam = new List<double>(){cross.girder_upper_flange_width*1000,
                cross.girder_lower_flange_width*1000,
                cross.girder_web_height*1000 };

            var plateThicknessList = new List<List<(double, double)>>()
                {
                 new List<(double, double)>(){(START + startGap, thickness[0]),(END-endGap,thickness[0]) },
                 new List<(double, double)>(){(START + startGap, thickness[1]),(END-endGap,thickness[1]) },
                 new List<(double, double)>(){(START + startGap, thickness[2]),(END-endGap,thickness[2]) }
                };
            using (var bridgeconstruction = new Bridge_Construction(INPATH, OUTPATH))
            {
                bridgeconstruction.SetGaps(startGap, endGap);
                bridgeconstruction.SetCrossSection(cross);
                bridgeconstruction.SetBridgeAlong(START, END,0,0);
                bridgeconstruction.SetOverallSection(sectionParam);
                //bridgeconstruction.CreatePlateList(END - endGap, cross);
                bridgeconstruction.SetThickness(plateThicknessList);

                bridgeconstruction.buildDeck();
            }

        }
        [TestMethod]
        public void BuildBridge_Construction_Test2()
        {
            const string INPATH = "../../TestFiles/alignment.ifc";
            const string OUTPATH = "../../TestFiles/aligment&construction1.ifc";
            var t1 = new Technical_Demand();
            var t2 = new CrossSection();
            t2.calculate_girder_parament(ref t1);
            var t3 = new longitudinal_lateral_connection();
            t3.get_parameters_truss(ref t1, ref t3, ref t2);
            var para = new Technical_Demand();
            var cross = new CrossSection();

            cross.calculate_girder_parament(ref para);
            cross.calculate_stiffener_info(cross.girder_web_height, cross.girder_web_thickness);

            t3.Calculate_stiffener_info(ref t2);

            //the parameter order is B_UPPER,B_LOWER,WEB
            const int START = 30000, END = 60000, STARTGAP = 20, ENDGAP = 20, LATOFFSET = 0;
            double VEROFFSET =-cross.vertical_offset_dis;
            var sectionParams = new List<double>() { cross.girder_upper_flange_width*1000, cross.girder_lower_flange_width * 1000, cross.girder_web_height * 1000 };
            var thickness = new List<List<(double, double)>>()
            {
                new List<(double, double)>(){(STARTGAP, cross.girder_upper_flange_thickness * 1000),(END - START - ENDGAP, cross.girder_upper_flange_thickness * 1000) },
                new List<(double, double)>(){(STARTGAP, cross.girder_lower_flange_thickness * 1000),(END - START - ENDGAP, cross.girder_lower_flange_thickness * 1000) },
                new List<(double, double)>(){(STARTGAP, cross.girder_web_thickness * 1000),(END - START - ENDGAP, cross.girder_web_thickness * 1000) }
            };

            using (var bridgeconstruction = new Bridge_Construction(INPATH, OUTPATH))
            {
                bridgeconstruction.SetCrossSection(cross);
                bridgeconstruction.SetBridgeAlong(START, END, VEROFFSET, LATOFFSET);
                bridgeconstruction.setLongitudinal_lateral_connection(t3);
                bridgeconstruction.SetGaps(STARTGAP, ENDGAP);
                bridgeconstruction.SetOverallSection(sectionParams);
                bridgeconstruction.SetThickness(thickness);
                bridgeconstruction.build();
            }
        }
        //public void BridgeBearingTest()
        //{
        //    const string INPATH = "../../TestFiles/alignment-arc.ifc";
        //    const string OUTPATH = "../../TestFiles/bearing.ifc";
        //    //using Alignment start & end data
        //    const int START = 30000, END = 60000;
        //    const int STARTGAP = 40, ENDGAP = 40;
        //    //generate all data for bridge
        //    var para = new Technical_Demand();
        //    //get all section parameters
        //    var cross = new CrossSection();
        //    cross.calculate_girder_parament(ref para);

        //    var sectionParam = new List<double>(){cross.girder_upper_flange_width*1000,
        //        cross.girder_lower_flange_width*1000,
        //        cross.girder_web_height*1000 };

        //    var thickness = cross.GetThickness();
        //    var plateThicknessList = new List<List<(double, double)>>()
        //        {
        //         new List<(double, double)>(){(END - ENDGAP, thickness[0]) },
        //         new List<(double, double)>(){(END - ENDGAP, thickness[1]) },
        //         new List<(double, double)>(){(END - ENDGAP, thickness[2]) }
        //        };

        //    using (var bridgeconstruction = new Bridge_Construction(INPATH, OUTPATH))
        //    {
        //        bridgeconstruction.SetBridgeAlong(START, END);
        //        bridgeconstruction.SetGaps(STARTGAP, ENDGAP);
        //        bridgeconstruction.SetOverallSection(sectionParam);

        //    }
        //}
        //[TestMethod]
        //public void BuildingBridgeBearingTest()
        //{
        //    const string INPATH = "../../TestFiles/alignment.ifc";
        //    const string OUTPATH = "../../TestFiles/Bearing.ifc";

        //}

        //ifc2mct Test is belowing
        [TestMethod]

        public void TranslateGirderTest()
        {
            const string PATH = "../../TestFiles/aligment&construction1.ifc";

            const string OUTPATH = "../../TestFiles/girder-test.mct";

            using (var model = IfcStore.Open(PATH))
            {
                var translateAdaptor = new TranslateAdaptor(PATH);
                translateAdaptor.TranslateGirder();
                translateAdaptor.TranslateLateralBeam();
                translateAdaptor.WriteMctFile(OUTPATH);
            }
        }
    }
}
