using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Common.Geometry;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;
using ifc2MCT_transferFactory.Models;

namespace Utilities
{
    public class TranslatorToolkitFactory
    {
        const int GIRDERPLATESUM = 3;
        public static List<MctMaterialValue> TranslateMaterial(List<IIfcMaterialSelect> girderSet_Material,ref int matIndex)
        {
            var mp = new Dictionary<string, double>();
            var material_Set = new List<MctMaterialValue>();
            for(int i=0;i<girderSet_Material.Count;i++)
            {
                var singleMP = (IIfcMaterial)girderSet_Material[i];
                foreach(var p_set in singleMP.HasProperties)
                {
                    foreach(var p in p_set.Properties)
                    {
                        if (p is IIfcPropertySingleValue pv && pv.NominalValue != null)
                            mp[pv.Name.ToString().ToUpper()] = (double)pv.NominalValue.Value;
                    }
                }
                var mat = new MctMaterialValue(new MctUnitSystem())
                {
                    Id = matIndex++,
                    Name = singleMP.Name,
                    Type = singleMP.Category.ToString().ToUpper() == "STEEL" ? MctMaterialTypeEnum.STEEL : (singleMP.Category.ToString().ToUpper() == "CONC" ? MctMaterialTypeEnum.CONC : MctMaterialTypeEnum.USER),
                    UseMass = true,
                    Elast = mp.ContainsKey("YOUNGMODULUS") ? mp["YOUNGMODULUS"] : 0,
                    Poisson = mp.ContainsKey("POISSONRATIO") ? mp["POISSONRATIO"] : 0,
                    Thermal = mp.ContainsKey("ThermalExpansionCoefficient") ? mp["ThermalExpansionCoefficient"] : 0,
                    Density=mp.ContainsKey("MASSDENSITY") ? mp["MASSDENSITY"] : 0,
                    Mass = mp.ContainsKey("MASSDENSITY") ? mp["MASSDENSITY"] : 0
                };
                material_Set.Add(mat);
            }
            return material_Set;
        }

        public static List<MctNode> TranslateNodes(IIfcCurve directrix, List<double> distAlong,int index)
        {
            var nodes = new List<MctNode>();
            if(directrix is IIfcOffsetCurveByDistances ocbd)
            {
                var basicCurve = ocbd.BasisCurve;
                if(basicCurve is IIfcAlignmentCurve ac)
                {
                    for(int i=0;i<distAlong.Count;i++)
                    {
                        double startDist = ocbd.OffsetValues[0].DistanceAlong + distAlong[i];
                        double offsetLateral = ocbd.OffsetValues[0].OffsetLateral.Value;
                        double offserVertical = ocbd.OffsetValues[0].OffsetVertical.Value;
                        var vz = new XbimVector3D(0, 0, 1);
                        double height = ac.Vertical.Segments[0].StartHeight;
                        var horSegs = ac.Horizontal.Segments;
                        (var pt, var vy) = Utilities.GeometryEngine.GetPointByDistAlong(horSegs, startDist);
                        var position = pt + vy * offsetLateral + vz * (offserVertical + height);
                        nodes.Add(new MctNode(index * distAlong.Count + i + 1, position.X, position.Y, position.Z));
                    }                    
                }
            }
            return nodes;
        }
        public static List<double> ParseSectionDimensions(IIfcElementAssembly girder)
        {
            var plateSolids = ParseIPlateSolids(girder);
            //var deckSolid = ParseDeckPlateSolid(deckPlate);

            double w1=0, w2=0, h=0, t1=0, t2=0, ht=0;

            if(plateSolids[0].CrossSections[0] is IIfcCenterLineProfileDef clp&&clp.Curve is IIfcPolyline pl)
            {
                w1 = Math.Abs(pl.Points[0].X - pl.Points[1].X);
                t1 = clp.Thickness;
            }

            if (plateSolids[2].CrossSections[0] is IIfcCenterLineProfileDef clp2 && clp2.Curve is IIfcPolyline pl2)
            {
                w2 = Math.Abs(pl2.Points[0].X - pl2.Points[1].X);
                t2 = clp2.Thickness;
            }

            if(plateSolids[1].CrossSections[1] is IIfcCenterLineProfileDef clp1 && clp1.Curve is IIfcPolyline pl1)
            {
                h = Math.Abs(pl1.Points[0].Y - pl1.Points[1].Y);
                ht = clp1.Thickness;
            }

            return new List<double>()
            {
                h,
                ht,
                w2,
                t2,
                w1,
                t1,
            };
        }

        public static List<IIfcSectionedSolidHorizontal> ParseIPlateSolids(IIfcElementAssembly girder)
        {
            var plateSolids = new List<IIfcSectionedSolidHorizontal>();
            var plates = girder.IsDecomposedBy.FirstOrDefault().RelatedObjects.
                Where(o => o is IIfcPlate p && (p.ObjectType == "FLANGE-PLATE" || p.ObjectType == "WEB-PLATE")).ToList();
            foreach (var plate in plates)
            {
                var plateItem = (IIfcPlate)plate;
                var item = plateItem.Representation.Representations[0].Items[0];
                if(item is IIfcSectionedSolidHorizontal ssh)
                {
                    plateSolids.Add(ssh);
                }
            }
            return plateSolids;
        }

        public static (double Bc, double Tc, double Hh) ParseDeckDimensions(IIfcSlab deck)
        {
            var deckSolid = ParseDeckPlateSolid(deck);
            (double Bc, double Tc, double Hh) deckInfo;
            deckInfo.Bc = 0;
            deckInfo.Tc = 0;
            deckInfo.Hh = 0;
            if(deckSolid.CrossSections[0] is IIfcArbitraryClosedProfileDef acp&&acp.OuterCurve is IIfcPolyline pl)
            {
                var pointSet = pl.Points.ToList();
                deckInfo.Bc = Math.Abs((pointSet[3].X + pointSet[4].X) / 2.0 - (pointSet[7].X + pointSet[8].X) / 2.0);
                deckInfo.Tc = Math.Abs(pointSet[0].Y - pointSet[1].Y);
                deckInfo.Hh = Math.Abs(pointSet[2].Y - pointSet[3].Y);
            }
            return deckInfo;
        }

        public static IIfcSectionedSolidHorizontal ParseDeckPlateSolid(IIfcSlab deck)
        {
            var deckSolid = deck.Representation.Representations[0].Items.FirstOrDefault();
            return (IIfcSectionedSolidHorizontal)deckSolid;
        }
        
        public static MctSectionCompsite_I TranslateSectionCompsite_I(IIfcElementAssembly girder,List<double> dim,
            (double Bc, double Tc, double Hh) deckInfo,int sectionId)
        {
            var dimensions = new List<double>(dim);
            var plateSolids = ParseIPlateSolids(girder);
            var sec = new MctSectionCompsite_I(dim,deckInfo)
            {
                Id = sectionId,
                Name = $"主梁",
                Type = "COMPOSITE",
                Offset = "CT,0,1,0,0,1,380",
            };
            return sec;
        }

        public static List<bool> TranslateBearing(IIfcProxy Bearing)
        {
            var constraint = new List<bool>();
            var values = Bearing.IsDefinedBy.
                Where(r => r.RelatingPropertyDefinition is IIfcPropertySet ps && ps.Name == "Pset_BearingCommon")
                .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties)
                .OfType<IIfcPropertyListValue>()
                .SelectMany(lv => lv.ListValues).ToList();
            foreach(var value in values)
            {
                if(value is IfcBoolean val)
                {
                    constraint.Add(val);
                }
            }
            return constraint;
        }

        //public static List<MctNode> TranslateNodes(List<IIfcElementAssembly> lateralBeamSet,int lateralBeamNums,ref int index)
        //{
        //    if (lateralBeamSet.Count < lateralBeamNums)
        //        throw new InvalidOperationException("Must have enough Beam to be operated");
        //    var nodes = new List<MctNode>();
        //    var BeamSet = new List<IIfcElementAssembly>();
        //    int girderNums = lateralBeamSet.Count / lateralBeamNums;

        //    for(int i=0;i<lateralBeamNums;i++)
        //    {
        //        var plateSet = lateralBeamSet[0].IsDecomposedBy.FirstOrDefault().RelatedObjects.Where(o => o is IIfcPlate).
        //            Select(o => (IIfcPlate)o).ToList();
        //        var solid1 = plateSet[0].Representation.Representations[0].Items.Where(o => o is IIfcExtrudedAreaSolid).
        //            Select(o => (IIfcExtrudedAreaSolid)o).FirstOrDefault();
        //        nodes.Add(new MctNode(index++, solid1.Position.Location.X, solid1.Position.Location.Y, solid1.Position.Location.Z));
        //        var solid2 = plateSet[2].Representation.Representations[0].Items.Where(o => o is IIfcExtrudedAreaSolid).
        //            Select(o => (IIfcExtrudedAreaSolid)o).FirstOrDefault();
        //        nodes.Add(new MctNode(index++, solid2.Position.Location.X, solid2.Position.Location.Y, solid2.Position.Location.Z));
        //    }

        //    for(int i=0;i<girderNums;i++)
        //    {
        //        for(int j=0; j<lateralBeamNums;j++)
        //        {
        //            var plateSet = lateralBeamSet[i*lateralBeamNums+j].IsDecomposedBy.FirstOrDefault().RelatedObjects.Where(o => o is IIfcPlate).
        //                Select(o => (IIfcPlate)o).ToList();
        //            var solid1 = plateSet[1].Representation.Representations[0].Items.Where(o => o is IIfcExtrudedAreaSolid).
        //                Select(o => (IIfcExtrudedAreaSolid)o).FirstOrDefault();
        //            nodes.Add(new MctNode(index++, solid1.Position.Location.X, solid1.Position.Location.Y, solid1.Position.Location.Z));
        //            var solid2 = plateSet[3].Representation.Representations[0].Items.Where(o => o is IIfcExtrudedAreaSolid).
        //                Select(o => (IIfcExtrudedAreaSolid)o).FirstOrDefault();
        //            nodes.Add(new MctNode(index++, solid2.Position.Location.X, solid1.Position.Location.Y, solid1.Position.Location.Z));
        //        }
        //    }
        //    return nodes;
        //}

        public static (double,double) ParseOffsetValues(List<IIfcElementAssembly> lateralBeamSet, int lateralBeamNums)
        {
            var plateSet = lateralBeamSet[0].IsDecomposedBy.FirstOrDefault().RelatedObjects.Where(o => o is IIfcPlate).
                Select(o => (IIfcPlate)o).ToList();
            var solid1 = plateSet[0].Representation.Representations[0].Items.Where(o => o is IIfcExtrudedAreaSolid).
                Select(o => (IIfcExtrudedAreaSolid)o).FirstOrDefault();
            (double locZ1, double locZ2) offsetValues;
            offsetValues.locZ1 = solid1.Position.Location.Z;
            var solid2 = plateSet[2].Representation.Representations[0].Items.Where(o => o is IIfcExtrudedAreaSolid).
                Select(o => (IIfcExtrudedAreaSolid)o).FirstOrDefault();
            offsetValues.locZ2 = solid2.Position.Location.Z;

            return offsetValues;
        }

        public static List<double> ParseLateralSection(IIfcElementAssembly lateralBeam)
        {
            var plateSolid = ParseLBeamSolid(lateralBeam);
            var dimensionSet = new List<double>();
            if(plateSolid is IIfcExtrudedAreaSolid ea&&ea.SweptArea is IIfcCenterLineProfileDef profile
                && profile.Curve is IIfcPolyline pl)
            {
                var profileNodeSet = pl.Points.ToList();
                dimensionSet.Add(Math.Abs(profileNodeSet[0].Y - profileNodeSet[1].Y));
                dimensionSet.Add(Math.Abs(profileNodeSet[1].X - profileNodeSet[2].X));
                dimensionSet.Add(profile.Thickness);
                dimensionSet.Add(profile.Thickness);
                dimensionSet.Add(2 * profileNodeSet[0].X);
            }
            return dimensionSet;
        }
        private static IIfcExtrudedAreaSolid ParseLBeamSolid(IIfcElementAssembly lateralBeam)
        {
            var plate = lateralBeam.IsDecomposedBy.FirstOrDefault().RelatedObjects.Select(o => (IIfcPlate)o).FirstOrDefault();
            return (IIfcExtrudedAreaSolid)plate.Representation.Representations[0].Items[0];
        }
    }
}
