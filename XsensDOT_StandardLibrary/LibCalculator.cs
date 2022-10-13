using System;
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
            Vector3 eulerAngles = new Vector3();
            Quaternion deltaQuaternion = Quaternion.Identity;
            //deltaQuaternion = quat1 * Quaternion.Inverse(quat2);
            // difference is obtained from the difference between the inverse(quat_parent) x quat_child) 
            deltaQuaternion = Quaternion.Inverse(quat1) * quat2;
            eulerAngles = ConvertQuaternionToDegreesEuler(deltaQuaternion);


            if (eulerAngles.X > 180) { eulerAngles.X -= 360.0f; }
            if (eulerAngles.Y > 180) { eulerAngles.Y -= 360.0f; }
            if (eulerAngles.Z > 180) { eulerAngles.Z -= 360.0f; }

            return eulerAngles;
        }

        /// <summary>
        /// Method to convert quaternion coordinates into euler coordinates (radians).
        /// Bit of a math method here from https://stackoverflow.com/questions/70462758/c-sharp-how-to-convert-quaternions-to-euler-angles-xyz
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
        /// Bit of a math method here from https://stackoverflow.com/questions/70462758/c-sharp-how-to-convert-quaternions-to-euler-angles-xyz
        /// </summary>
        public static Vector3 ConvertQuaternionToDegreesEuler(Quaternion q)
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
