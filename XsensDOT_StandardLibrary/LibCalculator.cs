﻿using System;
using System.Numerics;

namespace XsensDOT_StandardLibrary
{
    /// <summary>
    /// Contains all the mathematical operations for Joint Calculations for the Xsens DOT Inertial Measurement Units.
    /// </summary>
    public static class LibCalculator
    {
        /// <summary>
        /// Computes the joint angle in degrees given the two quaternion inputs.
        /// </summary>
        public static Vector3 ComputeJointAngle(Quaternion quat1, Quaternion quat2)
        {
            // sensor 1 = Proximal segment
            // sensor 2 = Distal segment
            //Quaternion deltaQuaternion = Quaternion.Conjugate(quat1) * quat2;
            Quaternion deltaQuaternion = Quaternion.Multiply(Quaternion.Conjugate(quat1), quat2);
            // adding a change for git

            

            Vector3 eulerAngles = ConvertQuaternionToEulerMethod2(deltaQuaternion);


            if (eulerAngles.X > 180) { eulerAngles.X -= 360.0f; }
            if (eulerAngles.Y > 180) { eulerAngles.Y -= 360.0f; }
            if (eulerAngles.Z > 180) { eulerAngles.Z -= 360.0f; }

            return eulerAngles;
        }

        /// <summary>
        /// Method to convert quaternion coordinates into euler coordinates (in degrees).
        /// Bit of a math method here from [insert source]
        /// </summary>
        public static Vector3 ConvertQuaternionToEulerMethod2(Quaternion q)
        {
            // roll (x-axis rotation)
            double sinRcosP = 2 * (q.W * q.X + q.Y * q.Z);
            double cosRcosP = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            double angleX = Math.Atan2(sinRcosP, cosRcosP);

            // pitch (y-axis rotation)
            double sinP = 2 * (q.W * q.Y - q.Z * q.X);
            double angleY = Math.Asin(sinP);

            /*
            if (Math.Abs(sinP) >= 1)
            {
                angleY = CopySign(Math.PI / 2, sinP);
            }
            else
            {
                angleY = Math.Asin(sinP);
            }
            */

            // yaw (z-axis rotation)
            double sinYcosP = 2 * (q.W * q.Z + q.X * q.Y);
            double cosYcosP = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            double angleZ = Math.Atan2(sinYcosP, cosYcosP);

            // convert to degrees
            angleX = 180.0 / Math.PI * angleX;
            angleY = 180.0f / Math.PI * angleY;
            angleZ = 180.0f / Math.PI * angleZ;

            // roundup values
            angleX = Math.Round(angleX, 6);
            angleY = Math.Round(angleY, 6);
            angleZ = Math.Round(angleZ, 6);

            return new Vector3((float)angleX, (float)angleY, (float)angleZ); ;
        }

        /// <summary>
        /// Method to mimic the copysign() method from C++. Method takes two arguments.
        /// Method returns the first argument with the sign of the second argument.
        /// https://cplusplus.com/reference/cmath/copysign/
        /// </summary>
        public static double CopySign(double arg1, double arg2)
        {
            return Math.Sign(arg2) * Math.Abs(arg1);
        }

        /// <summary>
        /// Method to convert quaternion coordinates into euler coordinates (in degrees).
        /// Bit of a math method here from https://stackoverflow.com/questions/70462758/c-sharp-how-to-convert-quaternions-to-euler-angles-xyz
        /// </summary>
        public static Vector3 ConvertQuaternionToEulerMethod1(Quaternion q)
        {
            // roll / x
            double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            double angleX = Math.Atan2(sinr_cosp, cosr_cosp);

            // pitch / y
            double angleY;
            double sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (Math.Abs(sinp) >= 1)
            {
                if (sinp >= 0) // positive
                {
                    angleY = Math.PI / 2;
                }
                else // she negative
                {
                    angleY = -Math.PI / 2;
                }
            }
            else
            {
                angleY = Math.Asin(sinp);
            }

            // yaw / z
            double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            double angleZ = Math.Atan2(siny_cosp, cosy_cosp);

            // Convert to degrees
            angleX = 180.0 / Math.PI * angleX;
            angleY = 180.0f / Math.PI * angleY;
            angleZ = 180.0f / (float)Math.PI * angleZ;

            // roundup the values
            angleX = Math.Round(angleX, 6);
            angleY = Math.Round(angleY, 6);
            angleZ = Math.Round(angleZ, 6);

            return new Vector3((float)angleX, (float)angleY, (float)angleZ);
        }



    }
}
