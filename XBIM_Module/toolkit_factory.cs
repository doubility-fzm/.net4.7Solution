using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.PresentationAppearanceResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.RepresentationResource;
using StructureDesignModule;

namespace XBIM_Module
{
    class toolkit_factory
    {
        public static IfcDirection MakeDirection(IfcStore m, double x = 0, double y = 0, double z = 0)
        {
            return m.Instances.New<IfcDirection>(d => d.SetXYZ(x, y, z));
        }

        public static IfcDirection MakeDirection(IfcStore m,IfcCartesianPoint p1,IfcCartesianPoint p2)
        {
            return m.Instances.New<IfcDirection>(d=>d.SetXYZ(p1.X-p2.X,
                p1.Y-p2.Y,p1.Z-p2.Z));
        }

        public static IfcAxis2Placement3D MakeAxis2Placement3D(IfcStore m, IfcCartesianPoint origin = null,
            IfcDirection localZ = null, IfcDirection localX = null)
        {
            return m.Instances.New<IfcAxis2Placement3D>(a =>
            {
                a.Location = origin ?? MakeCartesianPoint(m, 0, 0, 0);
                a.Axis = localZ;
                a.RefDirection = localX;
            });
        }

        public static IfcDirection MakeDirection(IfcStore m, XbimVector3D v)
        {
            return MakeDirection(m, v.X, v.Y, v.Z);
        }

        public static IfcSite CreateSite(IfcStore m, string name)
        {
            using (var txn = m.BeginTransaction("Create Site"))
            {
                var site = m.Instances.New<IfcSite>(s =>
                {
                    s.Name = name;
                    s.CompositionType = IfcElementCompositionEnum.ELEMENT;
                });
                var project = m.Instances.OfType<IfcProject>().FirstOrDefault();
                project.AddSite(site);
                txn.Commit();
                return site;
            }
        }

        public static void AddPrductIntoSpatial(IfcStore m, IfcSpatialStructureElement spatial, IfcProduct p, string txt)
        {
            using (var txn = m.BeginTransaction(txt))
            {
                spatial.AddElement(p);
                txn.Commit();
            }
        }
        public static IfcCurveSegment2D MakeLineSegment2D(IfcStore m, IfcCartesianPoint start, double dir, double length)
        {
            return m.Instances.New<IfcLineSegment2D>(s =>
            {
                s.StartPoint = start;
                s.StartDirection = dir;
                s.SegmentLength = length;
            });
        }

        public static IfcCircularArcSegment2D MakeCircleSeqment2D(IfcStore m,
            IfcCartesianPoint start, double dir, int length, int r, bool isccw)
        {
            return m.Instances.New<IfcCircularArcSegment2D>(cs =>
            {
                cs.StartPoint = start;
                cs.StartDirection = dir;
                cs.SegmentLength = length;
                cs.Radius = r;
                cs.IsCCW = isccw;
            });
        }

        public static IfcSectionedSolidHorizontal CreateSolidShapeBaseOnCurve(IfcStore m, IfcCurve directrix, double start, double end)
        {
            return m.Instances.New<IfcSectionedSolidHorizontal>(s =>
            {
                s.Directrix = directrix;
                var profile = MakeCircleProfile(m, 40);
                s.CrossSections.Add(profile);
                s.CrossSections.Add(profile);
                s.CrossSectionPositions.Add(MakeDistanceExpresstion(m, start));
                s.CrossSectionPositions.Add(MakeDistanceExpresstion(m, end));
            });
        }


        public static IfcCircleProfileDef MakeCircleProfile(IfcStore m, double diameter, IfcAxis2Placement2D Position = null)
        {
            return m.Instances.New<IfcCircleProfileDef>(cp =>
            {
                cp.ProfileType = IfcProfileTypeEnum.CURVE;
                if (Position != null)
                {
                    cp.Position = Position;
                }
                cp.Radius = diameter / 2;
            });
        }

        public static IfcDistanceExpression MakeDistanceExpresstion(IfcStore m, double distalong, double offlateral = 0, double offvertical = 0, bool alonghorizontal = true)
        {
            return m.Instances.New<IfcDistanceExpression>(dis =>
            {
                dis.DistanceAlong = distalong;
                dis.OffsetLateral = offlateral;
                dis.OffsetVertical = offvertical;
                dis.AlongHorizontal = alonghorizontal;
            });
        }

        public static void SetSurfaceColor(IfcStore m, IfcGeometricRepresentationItem geoitem, double red, double green, double blue, double transparency = 0)
        {
            var styleditem = m.Instances.New<IfcStyledItem>(si =>
            {
                si.Item = geoitem;
                si.Styles.Add(m.Instances.New<IfcSurfaceStyle>(ss =>
                {
                    ss.Side = IfcSurfaceSide.POSITIVE;
                    ss.Styles.Add(m.Instances.New<IfcSurfaceStyleRendering>(ssha =>
                    {
                        ssha.SurfaceColour = m.Instances.New<IfcColourRgb>(c =>
                        {
                            c.Red = red;
                            c.Green = green;
                            c.Blue = blue;
                        });
                        ssha.Transparency = transparency;
                    }));
                }));
            });
        }

        public static IfcShapeRepresentation MakeShapeRepresentation(IfcStore m, int dimention, string identifier, string type, IfcRepresentationItem item)
        {
            return m.Instances.New<IfcShapeRepresentation>(sr =>
            {
                sr.ContextOfItems = m.Instances.OfType<IfcGeometricRepresentationContext>()
                .Where(c => c.CoordinateSpaceDimension == 3)
                .FirstOrDefault();
                sr.RepresentationIdentifier = identifier;
                sr.RepresentationType = type;
                sr.Items.Add(item);
            });
        }

        //2019/8/4
        public static IfcCartesianPoint MakeCartesianPoint(IfcStore m, double x = 0, double y = 0, double z = 0)
        {
            return m.Instances.New<IfcCartesianPoint>(p => p.SetXYZ(x, y, z));
        }

        public static IfcPolyline MakePolyLine(IfcStore m, IfcCartesianPoint start, IfcCartesianPoint end)
        {
            return MakePolyLine(m, new List<IfcCartesianPoint>() { start, end });
        }

        public static IfcPolyline MakePolyLine(IfcStore m, List<IfcCartesianPoint> points)
        {
            return m.Instances.New<IfcPolyline>(pl =>
            {
                foreach (var point in points)
                {
                    pl.Points.Add(point);
                }
            });
        }

        public static IfcCenterLineProfileDef MakeCenterLineProfile(IfcStore m, IfcBoundedCurve curve, double thickness)
        {
            return m.Instances.New<IfcCenterLineProfileDef>(c =>
            {
                c.Thickness = thickness;
                c.Curve = curve;
            });
        }

        public static IfcCenterLineProfileDef MakeCenterLineProfile(IfcStore m, IfcCartesianPoint start, IfcCartesianPoint end,
            double thickness)
        {
            var line = MakePolyLine(m, start, end);
            return MakeCenterLineProfile(m, line, thickness);
        }

        public static IfcExtrudedAreaSolid MakeExtrudeAreaSolid(IfcStore m, IfcProfileDef profile, IfcAxis2Placement3D position,
            IfcDirection direction, IfcPositiveLengthMeasure depth)
        {
            return m.Instances.New<IfcExtrudedAreaSolid>(s =>
            {
                s.SweptArea = profile;
                s.Position = position;
                s.ExtrudedDirection = direction;
                s.Depth = depth;
            });
        }

        public static IfcShapeRepresentation MakeShapeRepresentation(IfcStore m,int dimention,string identifier,
            string type)
        {
            return m.Instances.New<IfcShapeRepresentation>(sr =>
            {
                sr.ContextOfItems = m.Instances.OfType<IfcGeometricRepresentationContext>()
                .Where(c => c.CoordinateSpaceDimension == dimention)
                .FirstOrDefault();
                sr.RepresentationIdentifier = identifier;
                sr.RepresentationType = type;
            });
        }

        public static IfcRectangleProfileDef MakeRectangleProfile(IfcStore m,double xDim,double yDim)
        {
            return m.Instances.New<IfcRectangleProfileDef>(prof =>
            {
                prof.XDim = xDim;
                prof.YDim = yDim;
                prof.ProfileType = IfcProfileTypeEnum.AREA;
            });
        }

        public static IfcExtrudedAreaSolid MakeExtrudedAreaSolid(IfcStore m,IfcProfileDef area,IfcAxis2Placement3D pos,
            IfcDirection dir,double depth)
        {
            return m.Instances.New<IfcExtrudedAreaSolid>(exSolid =>
            {
                exSolid.SweptArea = area;
                exSolid.Position = pos;
                exSolid.ExtrudedDirection = dir;
                exSolid.Depth = depth;
            });
        }

        public static IfcLinearPlacement MakeLinearPlacement(IfcStore m,IfcCurve constructAlign,
            IfcDistanceExpression dist,IfcOrientationExpression orientation=null)
        {
            return m.Instances.New<IfcLinearPlacement>(l =>
            {
                l.PlacementRelTo = constructAlign;
                l.Distance = dist;
                if (null != l.Orientation)
                {
                    l.Orientation = orientation;
                }
                l.CartesianPosition = ToAx3D(m, l);
            });
        }

        public static IfcLinearPlacement MakeLinearPlacementWithoutPoint(IfcStore m, IfcCurve constructAlign,
    IfcDistanceExpression dist, IfcOrientationExpression orientation = null)
        {
            return m.Instances.New<IfcLinearPlacement>(l =>
            {
                l.PlacementRelTo = constructAlign;
                l.Distance = dist;
                if (null != l.Orientation)
                {
                    l.Orientation = orientation;
                }
                l.CartesianPosition = ToAx3DWithoutLoc(m, l);
            });
        }

        public static IfcAxis2Placement3D ToAx3DWithoutLoc(IfcStore m, IfcLinearPlacement lp)
        {
            var origin = MakeCartesianPoint(m);
            var locZ = MakeDirection(m, 0, 0, 1);
            var locX = MakeDirection(m, 1, 0, 0);
            var curve = lp.PlacementRelTo;
            if (curve is IIfcOffsetCurveByDistances offsetCurve)
            {
                var basicCurve = offsetCurve.BasisCurve;
                double startDist = offsetCurve.OffsetValues[0].DistanceAlong + lp.Distance.DistanceAlong;
                double offsetLateral = offsetCurve.OffsetValues[0].OffsetLateral.Value + lp.Distance.OffsetLateral.Value;
                double offsetVertical = offsetCurve.OffsetValues[0].OffsetVertical.Value + lp.Distance.OffsetVertical.Value;
                if (basicCurve is IIfcAlignmentCurve ac)
                {
                    var vz = new XbimVector3D(0, 0, 1);
                    double height = ac.Vertical.Segments[0].StartHeight; // assume no slope
                    var horSegs = ac.Horizontal.Segments;
                    (var pt, var vy) = Utilities.GeometryEngine.GetPointByDistAlong(horSegs, startDist);
                    var position = vy * offsetLateral + vz * (offsetVertical);
                    var vx = vy.CrossProduct(vz);
                    origin = MakeCartesianPoint(m, position.X, position.Y, position.Z);
                    locX = MakeDirection(m, vx.X, vx.Y, vx.Z);
                }
            }
            return MakeAxis2Placement3D(m, origin, locZ, locX);
        }

        public static IfcLinearPlacement MakeLinearPlacement_LateralGirder(IfcStore m, IfcCurve constructAlign,
    IfcDistanceExpression dist, IfcOrientationExpression orientation = null)
        {
            return m.Instances.New<IfcLinearPlacement>(l =>
            {
                l.PlacementRelTo = constructAlign;
                l.Distance = dist;
                if (null != l.Orientation)
                {
                    l.Orientation = orientation;
                }
                l.CartesianPosition = ToAixs3D_LateralGirder(m, l);
            });
        }

        public static IfcLinearPlacement MakeLinearPlacement_LateralConnectPlate(IfcStore m, IfcCurve constructAlign,
IfcDistanceExpression dist, IfcOrientationExpression orientation = null)
        {
            return m.Instances.New<IfcLinearPlacement>(l =>
            {
                l.PlacementRelTo = constructAlign;
                l.Distance = dist;
                if (null != l.Orientation)
                {
                    l.Orientation = orientation;
                }
                l.CartesianPosition = ToAixs3D_LateralConnectPlate(m, l);
            });
        }

        public static IfcAxis2Placement3D ToAx3D(IfcStore m,IfcLinearPlacement lp)
        {
            var origin = MakeCartesianPoint(m);
            var locZ = MakeDirection(m, 0, 0, 1);
            var locX = MakeDirection(m, 1, 0, 0);
            var curve = lp.PlacementRelTo;
            if (curve is IIfcOffsetCurveByDistances offsetCurve)
            {
                var basicCurve = offsetCurve.BasisCurve;
                double startDist = offsetCurve.OffsetValues[0].DistanceAlong + lp.Distance.DistanceAlong;
                double offsetLateral = offsetCurve.OffsetValues[0].OffsetLateral.Value + lp.Distance.OffsetLateral.Value;
                double offsetVertical = offsetCurve.OffsetValues[0].OffsetVertical.Value + lp.Distance.OffsetVertical.Value;
                if (basicCurve is IIfcAlignmentCurve ac)
                {
                    var vz = new XbimVector3D(0, 0, 1);
                    double height = ac.Vertical.Segments[0].StartHeight; // assume no slope
                    var horSegs = ac.Horizontal.Segments;
                    (var pt, var vy) = Utilities.GeometryEngine.GetPointByDistAlong(horSegs, startDist);
                    var position = pt + vy * offsetLateral + vz * (offsetVertical + height);
                    var vx = vy.CrossProduct(vz);
                    origin = MakeCartesianPoint(m, position.X, position.Y, position.Z);
                    locX = MakeDirection(m, vx.X, vx.Y, vx.Z);
                }
            }
            return MakeAxis2Placement3D(m, origin, locZ, locX);
        }

        public static IfcAxis2Placement3D ToAixs3D_LateralGirder(IfcStore m,IfcLinearPlacement lp)
        {
            var origin = MakeCartesianPoint(m);
            var locZ = MakeDirection(m, 0, 0, -1);
            var locX = MakeDirection(m, 1, 0, 0);
            var locY = MakeDirection(m, 0, 1, 0);
            var curve = lp.PlacementRelTo;
            if(curve is IIfcOffsetCurveByDistances offsetCurve)
            {
                var basicCurve = offsetCurve.BasisCurve;
                double startDist = offsetCurve.OffsetValues[0].DistanceAlong + lp.Distance.DistanceAlong;
                double offsetLateral = offsetCurve.OffsetValues[0].OffsetLateral.Value + lp.Distance.OffsetLateral.Value;
                double offsetVertical = offsetCurve.OffsetValues[0].OffsetVertical.Value + lp.Distance.OffsetVertical.Value;
                if(basicCurve is IIfcAlignmentCurve ac)
                {
                    var vz = new XbimVector3D(0, 0, 1);
                    double height = ac.Vertical.Segments[0].StartHeight;
                    var horSegs = ac.Horizontal.Segments;
                    (var pt, var vy) = Utilities.GeometryEngine.GetPointByDistAlong(horSegs, startDist);
                    var vx = vz.CrossProduct(vy);
                    var position = pt + vy * offsetLateral + vz * (offsetVertical + height);
                    origin = MakeCartesianPoint(m, position.X, position.Y, position.Z);
                    locY = MakeDirection(m, vy.X, vy.Y, vy.Z);
                    locX = MakeDirection(m, vx.X, vx.Y, vx.Z);
                }
            }
            return MakeAxis2Placement3D(m, origin, locY, locX);
        }

        public static IfcAxis2Placement3D MakeInclineToAxis3D(IfcStore m,IfcLinearPlacement lp,IfcCartesianPoint p1,
            IfcCartesianPoint p2)
        {
            var origin = MakeCartesianPoint(m);
            var locZ = MakeDirection(m, p2, p1);
            var locX = MakeDirection(m, 1, 0, 0);
            var curve = lp.PlacementRelTo;
            if (curve is IIfcOffsetCurveByDistances offsetCurve)
            {
                var basicCurve = offsetCurve.BasisCurve;
                double startDist = offsetCurve.OffsetValues[0].DistanceAlong + lp.Distance.DistanceAlong;
                double offsetLateral = offsetCurve.OffsetValues[0].OffsetLateral.Value + lp.Distance.OffsetLateral.Value;
                double offsetVertical = offsetCurve.OffsetValues[0].OffsetVertical.Value + lp.Distance.OffsetVertical.Value;
                if (basicCurve is IIfcAlignmentCurve ac)
                {
                    var vz = new XbimVector3D(0, 0, 1);
                    double height = ac.Vertical.Segments[0].StartHeight; // assume no slope
                    var horSegs = ac.Horizontal.Segments;
                    (var pt, var vy) = Utilities.GeometryEngine.GetPointByDistAlong(horSegs, startDist);
                    var position = pt + vy * offsetLateral + vz * (offsetVertical + height);
                    var vx = vz.CrossProduct(vy);
                    origin = MakeCartesianPoint(m, position.X, position.Y, position.Z);
                    locX = MakeDirection(m, vx.X, vx.Y, vx.Z);
                }
            }
            return MakeAxis2Placement3D(m, origin, locZ, locX);
        }

        public static IfcAxis2Placement3D ToAixs3D_LateralConnectPlate(IfcStore m, IfcLinearPlacement lp)
        {
            var origin = MakeCartesianPoint(m);
            var locZ = MakeDirection(m, 0, 0, -1);
            var locX = MakeDirection(m, 1, 0, 0);
            var locY = MakeDirection(m, 0, 1, 0);
            var curve = lp.PlacementRelTo;
            if (curve is IIfcOffsetCurveByDistances offsetCurve)
            {
                var basicCurve = offsetCurve.BasisCurve;
                double startDist = offsetCurve.OffsetValues[0].DistanceAlong + lp.Distance.DistanceAlong;
                double offsetLateral = offsetCurve.OffsetValues[0].OffsetLateral.Value + lp.Distance.OffsetLateral.Value;
                double offsetVertical = offsetCurve.OffsetValues[0].OffsetVertical.Value + lp.Distance.OffsetVertical.Value;
                if (basicCurve is IIfcAlignmentCurve ac)
                {
                    var vz = new XbimVector3D(0, 0, 1);
                    double height = ac.Vertical.Segments[0].StartHeight;
                    var horSegs = ac.Horizontal.Segments;
                    (var pt, var vy) = Utilities.GeometryEngine.GetPointByDistAlong(horSegs, startDist);
                    var vx = vz.CrossProduct(vy);
                    var position = pt + vy * offsetLateral + vz * (offsetVertical + height);
                    origin = MakeCartesianPoint(m, position.X, position.Y, position.Z);
                    locY = MakeDirection(m, vy.X, vy.Y, vy.Z);
                    locX = MakeDirection(m, vx.X, vx.Y, vx.Z);
                }
            }
            return MakeAxis2Placement3D(m, origin, locX, locZ);
        }

        public static IfcArbitraryClosedProfileDef MakeBridgeDeckProfile(IfcStore m,CrossSection cross)
        {
            var DeckProfile = m.Instances.New<IfcArbitraryClosedProfileDef>();
            DeckProfile.ProfileType = IfcProfileTypeEnum.AREA;
            DeckProfile.ProfileName = "ConcreteDeck";

            var BoundedCurve = m.Instances.New<IfcPolyline>();
            var CurvePointsSet = new List<IfcCartesianPoint>();
            var p0 = MakeCartesianPoint(m, -cross.widthOfBridge / 2 * 1000, 0);
            // CurvePointsSet.Add(MakeCartesianPoint(m, -cross.widthOfBridge / 2 *1000, 0));
            CurvePointsSet.Add(p0);
            CurvePointsSet.Add(MakeCartesianPoint(m, -cross.widthOfBridge / 2 * 1000, -cross.vertical_offset_dis + 80));

            for (int i = 0; i < cross.lateral_offset_dis.Length; i++)
            {
                CurvePointsSet.Add(MakeCartesianPoint(m, cross.lateral_offset_dis[i] * 1000 - 240 - 1000 * cross.girder_upper_flange_width / 2, -cross.vertical_offset_dis + 80));
                CurvePointsSet.Add(MakeCartesianPoint(m, cross.lateral_offset_dis[i] * 1000 - 1000 * cross.girder_upper_flange_width / 2, -cross.vertical_offset_dis));
                CurvePointsSet.Add(MakeCartesianPoint(m, cross.lateral_offset_dis[i] * 1000 + 1000 * cross.girder_upper_flange_width / 2, -cross.vertical_offset_dis));
                CurvePointsSet.Add(MakeCartesianPoint(m, cross.lateral_offset_dis[i] * 1000 + 240 + 1000 * cross.girder_upper_flange_width / 2, -cross.vertical_offset_dis + 80));
            }

            CurvePointsSet.Add(MakeCartesianPoint(m, cross.widthOfBridge / 2 * 1000, -cross.vertical_offset_dis + 80));
            CurvePointsSet.Add(MakeCartesianPoint(m, cross.widthOfBridge / 2 * 1000, 0));
            CurvePointsSet.Add(p0);

            BoundedCurve = MakePolyLine(m, CurvePointsSet);
            DeckProfile.OuterCurve = BoundedCurve;
            return DeckProfile;
        }

        public static IfcArbitraryClosedProfileDef MakeLateralConnectPlateProfile(IfcStore m,
            (double flangewidth,double seal_Length,double gap,double web_height,double TH,int plateType)plateInfo)
        {
            var profile =m.Instances.New<IfcArbitraryClosedProfileDef>();
            profile.ProfileType = IfcProfileTypeEnum.AREA;
            profile.ProfileName = "横向联结系-接头板";

            var BoundedCurve = m.Instances.New<IfcPolyline>();
            var CurvePointsSet = new List<IfcCartesianPoint>();
            double w1 = plateInfo.flangewidth / 2 + plateInfo.gap + plateInfo.web_height * Math.Sin(plateInfo.TH)
                + plateInfo.seal_Length * Math.Cos(plateInfo.TH);
            double w2 = plateInfo.flangewidth / 2 + plateInfo.gap + plateInfo.seal_Length * Math.Cos(plateInfo.TH);
            double h1 = plateInfo.seal_Length * Math.Sin(plateInfo.TH) + plateInfo.web_height * Math.Cos(plateInfo.TH) +
                plateInfo.gap + plateInfo.web_height;
            double h2 = plateInfo.seal_Length * Math.Sin(plateInfo.TH) + plateInfo.gap + plateInfo.web_height;

            double h_l1 = plateInfo.web_height + plateInfo.gap + plateInfo.seal_Length;
            double h_l2 = plateInfo.web_height;
            double w_l1 = plateInfo.flangewidth / 2 + plateInfo.gap + plateInfo.seal_Length;
            double w_l2 = plateInfo.flangewidth / 2;

            double mid_w1 = plateInfo.gap + 2 * (plateInfo.seal_Length * Math.Cos(plateInfo.TH) +
                plateInfo.web_height * Math.Sin(plateInfo.TH));
            double mid_w2 = plateInfo.gap + plateInfo.seal_Length * 2 * Math.Cos(plateInfo.TH);
            double mid_h1 = plateInfo.seal_Length * Math.Sin(plateInfo.TH) + plateInfo.web_height * Math.Cos(plateInfo.TH)
                + plateInfo.gap + plateInfo.web_height;
            double mid_h2 = plateInfo.gap + plateInfo.seal_Length * Math.Sin(plateInfo.TH) + plateInfo.web_height;

            switch(plateInfo.plateType)
            {
                case 0:
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, 0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, -w1));
                    CurvePointsSet.Add(MakeCartesianPoint(m, h2 + 40, -w1));
                    CurvePointsSet.Add(MakeCartesianPoint(m, h1 + 40, -w2));
                    CurvePointsSet.Add(MakeCartesianPoint(m, h1 + 40, 0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, 0));
                    break;
                case 1:
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, 0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, w1));
                    CurvePointsSet.Add(MakeCartesianPoint(m, h2 + 40, w1));
                    CurvePointsSet.Add(MakeCartesianPoint(m, h1 + 40, w2));
                    CurvePointsSet.Add(MakeCartesianPoint(m, h1 + 40, 0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, 0));
                    break;
                case 2:
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, 0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, w_l1));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -h_l2, w_l1));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -h_l1, w_l2));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -h_l1, 0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, 0));
                    break;
                case 3:
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, 0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, -w_l1));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -h_l2, -w_l1));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -h_l1, -w_l2));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -h_l1, 0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, 0));
                    break;
                case 4:
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, -mid_w1 / 2 - 30));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -mid_h2 + 30, -mid_w1 / 2 - 30));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -mid_h1 + 30, -mid_w2 / 2 - 30));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -mid_h1 + 30, mid_w2 / 2 + 30));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -mid_h2 + 30, mid_w1 / 2 + 30));
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, mid_w1 / 2 + 30));
                    CurvePointsSet.Add(MakeCartesianPoint(m, 0, -mid_w1 / 2 - 30));
                    break;
            }

            //var point1 = MakeCartesianPoint(m, 0, 0);
            //CurvePointsSet.Add(point1);
            //var point2 = MakeCartesianPoint(m, w1, 0);
            //CurvePointsSet.Add(point2);
            //var point3 = MakeCartesianPoint(m, w1, -h2);
            //CurvePointsSet.Add(point3);
            //var point4 = MakeCartesianPoint(m, w2, -h1);
            //CurvePointsSet.Add(point4);
            //var point5 = MakeCartesianPoint(m, 0, -h1);
            //CurvePointsSet.Add(point5);
            //CurvePointsSet.Add(point1);

            BoundedCurve = MakePolyLine(m, CurvePointsSet);
            profile.OuterCurve = BoundedCurve;
            return profile;
        }

        public static IfcCenterLineProfileDef MakeLGirderProfile(IfcStore m,int gap,int thickness,int length,int Ltype)
        {
            var BoundedCurve = m.Instances.New<IfcPolyline>();
            var CurvePointsSet = new List<IfcCartesianPoint>();

            switch(Ltype)
            {
                case 0:
                    CurvePointsSet.Add(MakeCartesianPoint(m, gap / 2.0, -gap / 2.0 - length - thickness / 2.0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, gap / 2.0, -gap / 2.0 - thickness / 2.0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, thickness / 2.0 + length, -thickness / 2.0));
                    break;
                case 1:
                    CurvePointsSet.Add(MakeCartesianPoint(m, -gap / 2.0, -gap / 2.0 - length - thickness / 2.0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -gap / 2.0, -gap / 2.0 - thickness / 2.0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -thickness / 2.0 - length, -thickness / 2.0));
                    break;
                case 2:
                    CurvePointsSet.Add(MakeCartesianPoint(m, gap / 2.0, length + thickness / 2.0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, gap / 2.0, thickness / 2.0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, gap / 2.0 + thickness / 2.0 + length, thickness / 2.0));
                    break;
                case 3:
                    CurvePointsSet.Add(MakeCartesianPoint(m, -gap / 2.0, length + thickness / 2.0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -gap / 2.0, thickness / 2.0));
                    CurvePointsSet.Add(MakeCartesianPoint(m, -gap / 2.0 - thickness / 2.0 - length, thickness / 2.0));
                    break;
                default:
                    break;
            }

            BoundedCurve = MakePolyLine(m, CurvePointsSet);
            var profile = m.Instances.New<IfcCenterLineProfileDef>();
            profile.Thickness = thickness;
            profile.Curve = BoundedCurve;
            profile.ProfileType = IfcProfileTypeEnum.AREA;
            return profile;
        }

        public static IfcCartesianPoint MakeLateralGirderPoint(IfcStore m,IfcLinearPlacement lp)
        {
            var point = m.Instances.New<IfcCartesianPoint>();
            var curve = lp.PlacementRelTo;
            if(curve is IIfcOffsetCurveByDistances offsetCurve)
            {
                var basisCurve = offsetCurve.BasisCurve;
                double startDist = offsetCurve.OffsetValues[0].DistanceAlong + lp.Distance.DistanceAlong;
                double offsetLateral = offsetCurve.OffsetValues[0].OffsetLateral.Value + lp.Distance.OffsetLateral.Value;
                double offsetVertical = offsetCurve.OffsetValues[0].OffsetVertical.Value + lp.Distance.OffsetVertical.Value;
                if(basisCurve is IIfcAlignmentCurve ac)
                {
                    var vz = new XbimVector3D(0, 0, 1);
                    double height = ac.Vertical.Segments[0].StartHeight;
                    var horSegs = ac.Horizontal.Segments;
                    (var pt, var vy) = Utilities.GeometryEngine.GetPointByDistAlong(horSegs, startDist);
                    var vx = vy.CrossProduct(vz);
                    var position = pt + vy * offsetLateral + vz * (offsetVertical + height);
                    point.X = position.X;
                    point.Y = position.Y;
                    point.Z = position.Z;
                }
            }
            return point;
        }
        public static IfcArbitraryClosedProfileDef MakeClosedProfile(IfcStore m,List<IfcCartesianPoint> pointsSet)
        {
            var profile = m.Instances.New<IfcArbitraryClosedProfileDef>();
            profile.ProfileName = "Lateral Stiffener profile";
            profile.OuterCurve = MakePolyLine(m, pointsSet);
            return profile;
        }
        public static IfcLine MakeStraightLine(IfcStore m,IfcCartesianPoint start,IfcVector dir)
        {
            return m.Instances.New<IfcLine>(l =>
            {
                l.Pnt = start;
                l.Dir = dir;
            });
        }

    }
}
