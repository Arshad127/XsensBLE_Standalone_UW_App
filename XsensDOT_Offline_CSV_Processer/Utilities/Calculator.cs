using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace XsensDOT_Offline_CSV_Processer.Utilities
{
    /// <summary>
    /// Contains all the mathematical operations for Joint Calculations for the Xsens DOT Inertial Measurement Units.
    /// </summary>
    public static class Calculator
    {
        /// <summary>
        /// Computes the joint angle in degrees given the two quaternion inputs.
        /// </summary>
        public static Vector3 ComputeJointAngle(Quaternion quat1, Quaternion quat2)
        {
            Vector3 eulerAngles = new Vector3();
            Quaternion deltaQuaternion = Quaternion.Identity;
            deltaQuaternion = quat1 * Quaternion.Inverse(quat2);
            eulerAngles = ConvertQuaternionToDegreesEuler(deltaQuaternion);

            if (eulerAngles.X > 180) { eulerAngles.X -= 360.0f; }
            if (eulerAngles.Y > 180) { eulerAngles.Y -= 360.0f; }
            if (eulerAngles.Z > 180) { eulerAngles.Z -= 360.0f; }

            return eulerAngles;
        }

        /// <summary>
        /// Method to convert quaternion coordinates into euler coordinates (radians).
        /// </summary>
        public static Vector3 ConvertQuaternionToRadianEuler(Quaternion q)
        {
            Vector3 angleInRad = new Vector3();

            // roll / x
            double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            angleInRad.X = (float)Math.Atan2(sinr_cosp, cosr_cosp);

            // pitch / y
            double sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (Math.Abs(sinp) >= 1)
            {
                if (sinp >= 0) // positive
                {
                    angleInRad.Y = (float)Math.PI / 2;
                }
                else // she negative
                {
                    angleInRad.Y = -(float)Math.PI / 2;
                }
            }
            else
            {
                angleInRad.Y = (float)Math.Asin(sinp);
            }

            // yaw / z
            double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            angleInRad.Z = (float)Math.Atan2(siny_cosp, cosy_cosp);

            return angleInRad;
        }

        /// <summary>
        /// Method to convert quaternion coordinates into euler coordinates (degrees).
        /// </summary>
        public static Vector3 ConvertQuaternionToDegreesEuler(Quaternion q)
        {
            Vector3 angleInRad = new Vector3();
            Vector3 angleInDeg = new Vector3();

            // roll / x
            double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            angleInRad.X = (float)Math.Atan2(sinr_cosp, cosr_cosp);

            // pitch / y
            double sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (Math.Abs(sinp) >= 1)
            {
                if (sinp >= 0) // positive
                {
                    angleInRad.Y = (float)Math.PI / 2;
                }
                else // she negative
                {
                    angleInRad.Y = -(float)Math.PI / 2;
                }
            }
            else
            {
                angleInRad.Y = (float)Math.Asin(sinp);
            }

            // yaw / z
            double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            angleInRad.Z = (float)Math.Atan2(siny_cosp, cosy_cosp);

            // Convert to degrees
            angleInDeg.X = 180.0f / (float)Math.PI * angleInRad.X;
            angleInDeg.Y = 180.0f / (float)Math.PI * angleInRad.Y;
            angleInDeg.Z = 180.0f / (float)Math.PI * angleInRad.Z;

            return angleInDeg;
        }


    }
}
