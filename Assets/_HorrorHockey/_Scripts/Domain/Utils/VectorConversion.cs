using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharedVector2 = FM.Mathematics.DataTypes.Vector2;
using SharedVector3 = FM.Mathematics.DataTypes.Vector3;
public static class VectorConversion
{
    public static UnityEngine.Vector2 ToUnity(this SharedVector2 v)
        => new UnityEngine.Vector2(v.X, v.Y);
    public static UnityEngine.Vector3 ToUnity(this SharedVector3 v)
        => new UnityEngine.Vector3(v.X, v.Y, v.Z);
    public static ReadOnlySpan<UnityEngine.Vector2> AsUnity(this SharedVector2[] points)
    => MemoryMarshal.Cast<SharedVector2, UnityEngine.Vector2>(points);
    public static ReadOnlySpan<UnityEngine.Vector2> AsUnity(this ReadOnlySpan<SharedVector2> points)
        => MemoryMarshal.Cast<SharedVector2, UnityEngine.Vector2>(points);
    public static ReadOnlySpan<UnityEngine.Vector3> AsUnity(this SharedVector3[] points)
        => MemoryMarshal.Cast<SharedVector3, UnityEngine.Vector3>(points);
    public static ReadOnlySpan<UnityEngine.Vector3> AsUnity(this ReadOnlySpan<SharedVector3> points)
        => MemoryMarshal.Cast<SharedVector3, UnityEngine.Vector3>(points);
    public static ReadOnlySpan<SharedVector2> AsShared(this UnityEngine.Vector2[] points)
    => MemoryMarshal.Cast<UnityEngine.Vector2, SharedVector2>(points);
    public static ReadOnlySpan<SharedVector2> AsShared(this ReadOnlySpan<UnityEngine.Vector2> points)
        => MemoryMarshal.Cast<UnityEngine.Vector2, SharedVector2>(points);
    public static ReadOnlySpan<SharedVector3> AsShared(this UnityEngine.Vector3[] points)
        => MemoryMarshal.Cast<UnityEngine.Vector3, SharedVector3>(points);
    public static ReadOnlySpan<SharedVector3> AsShared(this ReadOnlySpan<UnityEngine.Vector3> points)
        => MemoryMarshal.Cast<UnityEngine.Vector3, SharedVector3>(points);

    public static List<UnityEngine.Vector2> AsUnity(this List<SharedVector2> points)
    {
        var result = new List<UnityEngine.Vector2>(points.Count);
        for (int i = 0; i < points.Count; i++)
            result.Add(points[i].ToUnity());
        return result;
    }

    public static List<UnityEngine.Vector3> AsUnity(this List<SharedVector3> points)
    {
        var result = new List<UnityEngine.Vector3>(points.Count);
        for (int i = 0; i < points.Count; i++)
            result.Add(points[i].ToUnity());
        return result;
    }

    public static List<SharedVector2> AsShared(this List<UnityEngine.Vector2> points)
    {
        var result = new List<SharedVector2>(points.Count);
        for (int i = 0; i < points.Count; i++)
            result.Add(new SharedVector2(points[i].x, points[i].y));
        return result;
    }

    public static List<SharedVector3> AsShared(this List<UnityEngine.Vector3> points)
    {
        var result = new List<SharedVector3>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            result.Add(new SharedVector3(point.x, point.y, point.z));
        }
        return result;
    }
}

// ReadOnlySpan<UnityEngine.Vector3> unityPoints = sharedPoints.AsUnity();