using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Ifc4.Interfaces;

namespace Utilities
{
    public class GeometryEngine
    {
        private static readonly double Tolerance = 1e-9;
        public static bool isSIUnits = true;
        #region BasicGeometricUtilities
        public static XbimVector3D ToXbimVector3D(IIfcDirection d)
        {
            return new XbimVector3D(d.X, d.Y, d.Z);
        }

        /// <summary>
        /// Compute the tangent by given angle in 2D plane.
        /// </summary>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static XbimVector3D ToVector3D(double angle)
        {
            const double Degree2Radian = Math.PI / 180;

            if (angle > 360 + Tolerance || angle < -360 - Tolerance) throw new ArgumentOutOfRangeException("angle");
            double angleRad = isSIUnits ? angle : angle * Degree2Radian;
            return new XbimVector3D(Math.Cos(angleRad), Math.Sin(angleRad), 0);
        }

        public static XbimPoint3D ToXbimPoint3D(IIfcCartesianPoint p)
        {
            if ((long)p.Dim.Value == 2) return new XbimPoint3D(p.X, p.Y, 0);
            return new XbimPoint3D(p.X, p.Y, p.Z);
        }

        public static XbimMatrix3D ToMatrix3D(IIfcAxis2Placement3D ax)
        {
            var origin = ToXbimPoint3D(ax.Location);
            var zAxis = ax.Axis == null ? new XbimVector3D(0, 0, 1) : ToXbimVector3D(ax.Axis).Normalized();
            var xAxis = ax.RefDirection == null ? new XbimVector3D(1, 0, 0) : ToXbimVector3D(ax.RefDirection).Normalized();
            var yAxis = zAxis.CrossProduct(xAxis);
            return new XbimMatrix3D(xAxis.X, xAxis.Y, xAxis.Z, 0, yAxis.X, yAxis.Y, yAxis.Z, 0,
                zAxis.X, zAxis.Y, zAxis.Z, 0, origin.X, origin.Y, origin.Z, 1);
        }

        public static XbimMatrix3D ToMatrix3D(IIfcObjectPlacement op)
        {
            // op must be an IfcLocalPlacement
            var lp = (IIfcLocalPlacement)op;
            var matrix = ToMatrix3D(lp.RelativePlacement as IIfcAxis2Placement3D);
            // If the attribute PlacementRelTo is null, then the local placement is given to the WCS.
            if (lp.PlacementRelTo == null) return matrix;
            // Recursively deal with the transformation
            return matrix * ToMatrix3D(lp.PlacementRelTo);
        }
        #endregion
        #region GetPointOnCurve
        // 已知曲线(直线、圆曲线)和其上一点距曲线起点的沿线距离, 
        // 计算该点的坐标, 以及该点切向的垂直方向向量lateral
        public static (XbimPoint3D pt, XbimVector3D vec) GetPointOnCurve(IIfcCurveSegment2D c, double dist)
        {
            if (c is IIfcLineSegment2D line)
                return GetPointOnCurve(line, dist);
            if (c is IIfcCircularArcSegment2D arc)
                return GetPointOnCurve(arc, dist);
            throw new NotImplementedException("Transition curve not supported for now");
        }

        // 已知直线和其上一点距直线起点的沿线距离
        public static (XbimPoint3D pt, XbimVector3D vec) GetPointOnCurve(IIfcLineSegment2D l, double dist)
        {
            var length = l.SegmentLength;
            if (dist > length + Tolerance) throw new ArgumentOutOfRangeException("dist");
            var start = new XbimPoint3D(l.StartPoint.X, l.StartPoint.Y, 0);
            var startDir = ToVector3D((double)l.StartDirection.Value);
            var zAxis = new XbimVector3D(0, 0, 1);
            var lateral = zAxis.CrossProduct(startDir);
            return (start + dist * startDir, lateral);
        }

        // 已知圆曲线和其上一点距圆曲线起点的沿线距离
        public static (XbimPoint3D pt, XbimVector3D vec) GetPointOnCurve(IIfcCircularArcSegment2D arc, double dist)
        {
            var length = (double)arc.SegmentLength.Value;
            if (dist > length + Tolerance) throw new ArgumentOutOfRangeException("dist");
            var start = new XbimPoint3D(arc.StartPoint.X, arc.StartPoint.Y, 0);
            var radius = (double)arc.Radius.Value;
            var isCCW = (bool)arc.IsCCW.Value;

            // 计算起点切线方向向量
            var startDir = ToVector3D((double)arc.StartDirection.Value);
            return GetPointOnCurve(start, startDir, radius, isCCW, dist);
        }

        /// <summary>
        /// Get point on arc by given start point, start direction, arc radius, 
        /// is counter-clockwise and distance along.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="dir"></param>
        /// <param name="radius"></param>
        /// <param name="isCCW"></param>
        /// <param name="dist"></param>
        /// <returns></returns>
        public static (XbimPoint3D pt, XbimVector3D vec) GetPointOnCurve(XbimPoint3D start, XbimVector3D dir,
            double radius, bool isCCW, double dist)
        {
            // Compute the location of arc center.
            var zAxis = new XbimVector3D(0, 0, 1);
            var start2center = isCCW ? zAxis.CrossProduct(dir) : dir.CrossProduct(zAxis);
            var center = start + radius * start2center;

            // Compute the location of arc end point
            var theta = isCCW ? dist / radius : -dist / radius;
            var center2start = start2center.Negated();
            var mat = new XbimMatrix3D();
            mat.RotateAroundZAxis(theta);
            var center2end = mat.Transform(center2start);
            var lateral = isCCW ? center2end.Negated() : center2end;
            return (center + radius * center2end, lateral);
        }

        public static (XbimPoint3D pt, XbimVector3D vec) GetPointByDistAlong(IItemSet<IIfcAlignment2DHorizontalSegment> horSegs, double dist)
        {
            int i = 0;
            IIfcCurveSegment2D cur = null;
            for (; i < horSegs.Count; i++)
            {
                cur = horSegs[i].CurveGeometry;
                if (dist > cur.SegmentLength + Tolerance)
                    dist -= cur.SegmentLength;
                else break;
            }
            if (cur == null) throw new ArgumentNullException("horSegs");
            return GetPointOnCurve(cur, dist);
        }

        #endregion


        public static XbimPoint3D GetPlacementPoint(IIfcCurveSegment2D c, double dist,
            double verticalOffset, double lateraloffset)
        {
            XbimPoint3D Point3D=new XbimPoint3D();
            if (c is IIfcLineSegment2D line)
            {
                (XbimPoint3D pointOnCurve, XbimVector3D vecOnCurve) = GetPointOnCurve(line, dist);
                var zAxis = new XbimVector3D(0, 0, 1);
                var vecoff = vecOnCurve.CrossProduct(zAxis);
                Point3D = (pointOnCurve + vecoff * lateraloffset+ zAxis * verticalOffset);
                //return Point3D;
            }
            if(c is IIfcCircularArcSegment2D arc)
            {
                (XbimPoint3D pointOnCurve, XbimVector3D vecOnCurve) = GetPointOnCurve(arc, dist);
                var isCCW = (bool)arc.IsCCW.Value;
                var zAxis = new XbimVector3D(0, 0, 1);
                var start2center = isCCW ? zAxis.CrossProduct(vecOnCurve) : vecOnCurve.CrossProduct(zAxis);
                Point3D = (pointOnCurve + start2center * lateraloffset + zAxis * verticalOffset);
                //return Point3D;
            }
            return Point3D;
        }
    }
}
