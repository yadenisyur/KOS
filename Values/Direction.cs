﻿using System;
using UnityEngine;

namespace kOS.Values
{
    public class Direction : SpecialValue
    {
        private Vector3d vector;
        public Vector3d Vector
        {
            get { return vector; }
            set 
            { 
                vector = value; rotation = Quaternion.LookRotation(value); euler = rotation.eulerAngles; 
            }
        }

        private Vector3d euler;
        public Vector3d Euler
        {
            get { return euler; }
            set 
            { 
                euler = value; rotation = Quaternion.Euler(value);
            }
        }

        private Quaternion rotation;
        public Quaternion Rotation
        {
            get { return rotation; }
            set { rotation = value; euler = value.eulerAngles; }
        }

        public Direction()
        {
        }

        public Direction(Quaternion q)
        {
            rotation = q;
            euler = q.eulerAngles;
        }

        public Direction(Vector3d vector, bool isEuler)
        {
            if (isEuler)
            {
                Euler = vector;
            }
            else
            {
                Vector = vector; 
            }
        }

        public override object GetSuffix(string suffixName)
        {
            switch (suffixName)
            {
                case "PITCH":
                    return euler.x;
                case "YAW":
                    return euler.y;
                case "ROLL":
                    return euler.z;
                case "VECTOR":
                    return new Vector(vector);
                default:
                    return null;
            }
        }

        public void RedefineUp(Vector3d up)
        {
        }

        public static Direction operator *(Direction a, Direction b) { return new Direction(a.Rotation * b.Rotation); }
        public static Direction operator +(Direction a, Direction b) { return new Direction(a.Euler + b.Euler, true); }
        public static Direction operator -(Direction a, Direction b) { return new Direction(a.Euler - b.Euler, true); }
        
        public override object TryOperation(string op, object other, bool reverseOrder)
        {
            if (other is Vector)
            {
                other = ((Vector)other).ToDirection();
            }

            if (op == "*" && other is Direction)
            {
                // If I remember correctly, order of multiplication DOES matter with quaternions
                if (!reverseOrder)
                    return this * (Direction)other;
                return (Direction)other * this;
            }
            if (op == "+" && other is Direction) return this + (Direction)other;
            if (op == "-" && other is Direction)
            {
                if (!reverseOrder)
                    return this - (Direction)other;
                return (Direction)other - this;
            }

            return null;
        }

        public override string ToString()
        {
            return "R(" + Math.Round(euler.x, 3) + "," + Math.Round(euler.y, 3) + "," + Math.Round(euler.z, 3) + ")";
        }
    }
}