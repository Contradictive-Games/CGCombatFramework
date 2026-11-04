using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal enum SvgOp
    {
        Move,
        Line,
        Quad,
        Cubic,
        Close
    }

    internal readonly struct SvgSegment
    {
        public readonly SvgOp Op;
        public readonly Vector2 P1;
        public readonly Vector2 P2;
        public readonly Vector2 P3;

        public SvgSegment(SvgOp op, Vector2 p1 = default, Vector2 p2 = default, Vector2 p3 = default)
        {
            Op = op;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        public Vector2 End
        {
            get
            {
                switch(Op)
                {
                    case SvgOp.Cubic: return P3;
                    case SvgOp.Quad: return P2;
                    default: return P1;
                }
            }
        }
    }

    internal sealed class SvgShape
    {
        public readonly SvgSegment[] Segments;
        public readonly Rect Bounds;

        public SvgShape(SvgSegment[] segments, Rect bounds)
        {
            Segments = segments;
            Bounds = bounds;
        }

        public Vector2 Map(Vector2 point, Rect area) => area.center + (point - Bounds.center) * ScaleFor(area);

        public void Draw(Painter2D painter, Rect area, bool outline)
        {
            float scale = ScaleFor(area);
            var offset = area.center - Bounds.center * scale;
            bool open = false;
            painter.BeginPath();
            for(int i = 0; i < Segments.Length; i++)
            {
                var each = Segments[i];
                switch(each.Op)
                {
                    case SvgOp.Move:
                        if(open && !outline) painter.ClosePath();
                        painter.MoveTo(each.P1 * scale + offset);
                        open = true;
                        break;
                    case SvgOp.Line:
                        painter.LineTo(each.P1 * scale + offset);
                        break;
                    case SvgOp.Quad:
                        painter.QuadraticCurveTo(each.P1 * scale + offset, each.P2 * scale + offset);
                        break;
                    case SvgOp.Cubic:
                        painter.BezierCurveTo(each.P1 * scale + offset, each.P2 * scale + offset, each.P3 * scale + offset);
                        break;
                    case SvgOp.Close:
                        painter.ClosePath();
                        open = false;
                        break;
                }
            }
            if(outline)
            {
                painter.Stroke();
                return;
            }
            if(open) painter.ClosePath();
            painter.Fill(FillRule.NonZero);
        }

        private float ScaleFor(Rect area)
        {
            float x = Bounds.width > 0f ? area.width / Bounds.width : float.PositiveInfinity;
            float y = Bounds.height > 0f ? area.height / Bounds.height : float.PositiveInfinity;
            return Mathf.Min(x, y);
        }
    }

    internal static class SvgPath
    {
        private const string k_Commands = "MmLlHhVvCcSsQqTtAaZz";
        private const double k_Quarter = Math.PI * 0.5;

        public static bool TryParse(string data, out SvgShape shape)
        {
            shape = null;
            if(string.IsNullOrWhiteSpace(data)) return false;

            var segments = new List<SvgSegment>();
            var current = Vector2.zero;
            var start = Vector2.zero;
            var control = Vector2.zero;
            char command = '\0';
            char previous = '\0';
            bool closed = false;
            int i = 0;

            while(true)
            {
                Skip(data, ref i);
                if(i >= data.Length) break;

                char c = data[i];
                if(char.IsLetter(c) && c != 'e' && c != 'E')
                {
                    if(k_Commands.IndexOf(c) < 0) return false;
                    command = c;
                    i++;
                }
                else if(command == '\0' || command == 'Z' || command == 'z')
                {
                    return false;
                }
                else if(command == 'M')
                {
                    command = 'L';
                }
                else if(command == 'm')
                {
                    command = 'l';
                }

                char upper = char.ToUpperInvariant(command);
                if(segments.Count == 0 && upper != 'M') return false;
                var origin = char.IsLower(command) ? current : Vector2.zero;

                if(upper == 'Z')
                {
                    segments.Add(new SvgSegment(SvgOp.Close));
                    current = start;
                    closed = true;
                    previous = upper;
                    continue;
                }

                if(upper == 'M')
                {
                    if(!Point(data, ref i, out var to)) return false;
                    current = start = origin + to;
                    segments.Add(new SvgSegment(SvgOp.Move, current));
                    closed = false;
                    previous = upper;
                    continue;
                }

                if(closed)
                {
                    segments.Add(new SvgSegment(SvgOp.Move, start));
                    closed = false;
                }

                switch(upper)
                {
                    case 'L':
                    {
                        if(!Point(data, ref i, out var to)) return false;
                        current = origin + to;
                        segments.Add(new SvgSegment(SvgOp.Line, current));
                        break;
                    }
                    case 'H':
                    {
                        if(!Number(data, ref i, out float x)) return false;
                        current = new Vector2(origin.x + x, current.y);
                        segments.Add(new SvgSegment(SvgOp.Line, current));
                        break;
                    }
                    case 'V':
                    {
                        if(!Number(data, ref i, out float y)) return false;
                        current = new Vector2(current.x, origin.y + y);
                        segments.Add(new SvgSegment(SvgOp.Line, current));
                        break;
                    }
                    case 'C':
                    {
                        if(!Point(data, ref i, out var c1) || !Point(data, ref i, out var c2) || !Point(data, ref i, out var to)) return false;
                        control = origin + c2;
                        current = origin + to;
                        segments.Add(new SvgSegment(SvgOp.Cubic, origin + c1, control, current));
                        break;
                    }
                    case 'S':
                    {
                        if(!Point(data, ref i, out var c2) || !Point(data, ref i, out var to)) return false;
                        var c1 = previous == 'C' || previous == 'S' ? current * 2f - control : current;
                        control = origin + c2;
                        current = origin + to;
                        segments.Add(new SvgSegment(SvgOp.Cubic, c1, control, current));
                        break;
                    }
                    case 'Q':
                    {
                        if(!Point(data, ref i, out var c1) || !Point(data, ref i, out var to)) return false;
                        control = origin + c1;
                        current = origin + to;
                        segments.Add(new SvgSegment(SvgOp.Quad, control, current));
                        break;
                    }
                    case 'T':
                    {
                        if(!Point(data, ref i, out var to)) return false;
                        control = previous == 'Q' || previous == 'T' ? current * 2f - control : current;
                        current = origin + to;
                        segments.Add(new SvgSegment(SvgOp.Quad, control, current));
                        break;
                    }
                    case 'A':
                    {
                        if(!Number(data, ref i, out float rx) || !Number(data, ref i, out float ry) || !Number(data, ref i, out float rotation)) return false;
                        if(!Flag(data, ref i, out bool large) || !Flag(data, ref i, out bool sweep) || !Point(data, ref i, out var to)) return false;
                        var end = origin + to;
                        Arc(segments, current, rx, ry, rotation, large, sweep, end);
                        current = end;
                        break;
                    }
                }
                previous = upper;
            }

            if(!TryBounds(segments, out var bounds)) return false;
            shape = new SvgShape(segments.ToArray(), bounds);
            return true;
        }

        private static void Skip(string data, ref int i)
        {
            while(i < data.Length && (char.IsWhiteSpace(data[i]) || data[i] == ','))
            {
                i++;
            }
        }

        private static bool Digit(char c) => c >= '0' && c <= '9';

        private static bool Number(string data, ref int i, out float value)
        {
            value = 0f;
            Skip(data, ref i);
            int begin = i;
            if(i < data.Length && (data[i] == '+' || data[i] == '-')) i++;
            bool digits = false;
            while(i < data.Length && Digit(data[i]))
            {
                i++;
                digits = true;
            }
            if(i < data.Length && data[i] == '.')
            {
                i++;
                while(i < data.Length && Digit(data[i]))
                {
                    i++;
                    digits = true;
                }
            }
            if(!digits)
            {
                i = begin;
                return false;
            }
            if(i < data.Length && (data[i] == 'e' || data[i] == 'E'))
            {
                int mark = i++;
                if(i < data.Length && (data[i] == '+' || data[i] == '-')) i++;
                int exponent = i;
                while(i < data.Length && Digit(data[i]))
                {
                    i++;
                }
                if(i == exponent) i = mark;
            }
            return float.TryParse(data.Substring(begin, i - begin), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool Point(string data, ref int i, out Vector2 point)
        {
            point = default;
            if(!Number(data, ref i, out float x) || !Number(data, ref i, out float y)) return false;
            point = new Vector2(x, y);
            return true;
        }

        private static bool Flag(string data, ref int i, out bool value)
        {
            value = false;
            Skip(data, ref i);
            if(i >= data.Length || (data[i] != '0' && data[i] != '1')) return false;
            value = data[i] == '1';
            i++;
            return true;
        }

        private static void Arc(List<SvgSegment> segments, Vector2 from, float rx, float ry, float rotation, bool large, bool sweep, Vector2 to)
        {
            if(from == to) return;
            double radiusX = Math.Abs(rx);
            double radiusY = Math.Abs(ry);
            if(radiusX < 1e-6 || radiusY < 1e-6)
            {
                segments.Add(new SvgSegment(SvgOp.Line, to));
                return;
            }

            double phi = rotation * Math.PI / 180.0;
            double cos = Math.Cos(phi);
            double sin = Math.Sin(phi);
            double dx = (from.x - to.x) * 0.5;
            double dy = (from.y - to.y) * 0.5;
            double x1 = cos * dx + sin * dy;
            double y1 = -sin * dx + cos * dy;

            double lambda = x1 * x1 / (radiusX * radiusX) + y1 * y1 / (radiusY * radiusY);
            if(lambda > 1.0)
            {
                double grow = Math.Sqrt(lambda);
                radiusX *= grow;
                radiusY *= grow;
            }

            double rx2 = radiusX * radiusX;
            double ry2 = radiusY * radiusY;
            double denominator = rx2 * y1 * y1 + ry2 * x1 * x1;
            double coefficient = denominator > 0.0 ? Math.Sqrt(Math.Max(0.0, (rx2 * ry2 - denominator) / denominator)) : 0.0;
            if(large == sweep) coefficient = -coefficient;
            double cx1 = coefficient * radiusX * y1 / radiusY;
            double cy1 = -coefficient * radiusY * x1 / radiusX;
            double cx = cos * cx1 - sin * cy1 + (from.x + to.x) * 0.5;
            double cy = sin * cx1 + cos * cy1 + (from.y + to.y) * 0.5;

            double ux = (x1 - cx1) / radiusX;
            double uy = (y1 - cy1) / radiusY;
            double vx = (-x1 - cx1) / radiusX;
            double vy = (-y1 - cy1) / radiusY;
            double theta = Angle(1.0, 0.0, ux, uy);
            double delta = Angle(ux, uy, vx, vy);
            if(!sweep && delta > 0.0) delta -= Math.PI * 2.0;
            else if(sweep && delta < 0.0) delta += Math.PI * 2.0;

            int count = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / k_Quarter - 1e-9));
            double step = delta / count;
            double k = 4.0 / 3.0 * Math.Tan(step / 4.0);

            Vector2 On(double u, double v) => new((float)(cx + radiusX * u * cos - radiusY * v * sin), (float)(cy + radiusX * u * sin + radiusY * v * cos));

            for(int n = 0; n < count; n++)
            {
                double a1 = theta + n * step;
                double a2 = a1 + step;
                double cos1 = Math.Cos(a1);
                double sin1 = Math.Sin(a1);
                double cos2 = Math.Cos(a2);
                double sin2 = Math.Sin(a2);
                var end = n == count - 1 ? to : On(cos2, sin2);
                segments.Add(new SvgSegment(SvgOp.Cubic, On(cos1 - k * sin1, sin1 + k * cos1), On(cos2 + k * sin2, sin2 - k * cos2), end));
            }
        }

        private static double Angle(double ux, double uy, double vx, double vy) => Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);

        private static bool TryBounds(List<SvgSegment> segments, out Rect bounds)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var current = Vector2.zero;
            var start = Vector2.zero;
            bool drawn = false;

            foreach(var each in segments)
            {
                if(each.Op == SvgOp.Move)
                {
                    current = start = each.P1;
                    continue;
                }
                if(each.Op == SvgOp.Close)
                {
                    current = start;
                    continue;
                }

                drawn = true;
                Include(ref min, ref max, current);
                Include(ref min, ref max, each.End);
                for(int axis = 0; axis < 2; axis++)
                {
                    if(each.Op == SvgOp.Quad)
                    {
                        float t = QuadTurn(current[axis], each.P1[axis], each.P2[axis]);
                        if(Inside(t)) Include(ref min, ref max, Quad(current, each.P1, each.P2, t));
                    }
                    else if(each.Op == SvgOp.Cubic)
                    {
                        CubicTurns(current[axis], each.P1[axis], each.P2[axis], each.P3[axis], out float first, out float second);
                        if(Inside(first)) Include(ref min, ref max, Cubic(current, each.P1, each.P2, each.P3, first));
                        if(Inside(second)) Include(ref min, ref max, Cubic(current, each.P1, each.P2, each.P3, second));
                    }
                }
                current = each.End;
            }

            bounds = drawn ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
            return drawn && (bounds.width > 0f || bounds.height > 0f) && !float.IsNaN(bounds.width) && !float.IsInfinity(bounds.width) && !float.IsInfinity(bounds.height);
        }

        private static void Include(ref Vector2 min, ref Vector2 max, Vector2 point)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        private static bool Inside(float t) => t > 0f && t < 1f;

        private static float QuadTurn(float p0, float p1, float p2)
        {
            float denominator = p0 - 2f * p1 + p2;
            return Mathf.Abs(denominator) < 1e-9f ? -1f : (p0 - p1) / denominator;
        }

        private static void CubicTurns(float p0, float p1, float p2, float p3, out float first, out float second)
        {
            first = -1f;
            second = -1f;
            float a = -p0 + 3f * p1 - 3f * p2 + p3;
            float b = 2f * (p0 - 2f * p1 + p2);
            float c = p1 - p0;
            if(Mathf.Abs(a) < 1e-9f)
            {
                if(Mathf.Abs(b) > 1e-9f) first = -c / b;
                return;
            }
            float discriminant = b * b - 4f * a * c;
            if(discriminant < 0f) return;
            float root = Mathf.Sqrt(discriminant);
            first = (-b + root) / (2f * a);
            second = (-b - root) / (2f * a);
        }

        private static Vector2 Quad(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private static Vector2 Cubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }
    }
}
