using HelixToolkit.Geometry;
using HelixToolkit.Wpf;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace capstoneOneShot.Services
{
    /// <summary>
    /// Draws a stylized 3D human figure driven by Kinect skeleton data.
    /// Works with any HelixViewport3D - pass the viewport in and call
    /// Update() every skeleton frame.
    /// </summary>
    public class AvatarService
    {
        private const float PositionSmoothing = 0.35f;

        private readonly HelixViewport3D _viewport;
        private readonly Dictionary<string, ModelVisual3D> _segments
            = new Dictionary<string, ModelVisual3D>();
        private readonly Dictionary<JointType, Vector3> _smoothedJointPositions
            = new Dictionary<JointType, Vector3>();

        private static readonly Color TorsoColor = ParseColor("#B0BEC5");
        private static readonly Color ArmColor = ParseColor("#90CAF9");
        private static readonly Color LegColor = ParseColor("#80CBC4");
        private static readonly Color HeadColor = ParseColor("#E0E0E0");
        private static readonly Color ActiveHandColor = ParseColor("#4DD0E1");
        private static readonly Color JointColor = ParseColor("#CFD8DC");

        public AvatarService(HelixViewport3D viewport)
        {
            _viewport = viewport;
            AddLights();
        }

        // Public API

        public void Update(Skeleton skeleton)
        {
            if (skeleton == null || skeleton.TrackingState != SkeletonTrackingState.Tracked)
            {
                HideAllSegments();
                return;
            }

            EnsureSegmentsBuilt();
            var j = skeleton.Joints;

            bool leftHigher = GetSmoothedPosition(JointType.HandLeft, ToVector3(j[JointType.HandLeft].Position)).Y
                            > GetSmoothedPosition(JointType.HandRight, ToVector3(j[JointType.HandRight].Position)).Y;
            JointType activeHandType = leftHigher ? JointType.HandLeft : JointType.HandRight;

            // Torso
            DrawCylinder("ShoulderLine", j[JointType.ShoulderLeft], j[JointType.ShoulderRight], 0.04f, TorsoColor);
            DrawCylinder("Neck", j[JointType.ShoulderCenter], j[JointType.Head], 0.045f, TorsoColor);
            DrawCylinder("Spine", j[JointType.ShoulderCenter], j[JointType.HipCenter], 0.07f, TorsoColor);
            DrawCylinder("HipLine", j[JointType.HipLeft], j[JointType.HipRight], 0.04f, TorsoColor);

            // Arms
            DrawCylinder("UpperArmL", j[JointType.ShoulderLeft], j[JointType.ElbowLeft], 0.05f, ArmColor);
            DrawCylinder("LowerArmL", j[JointType.ElbowLeft], j[JointType.WristLeft], 0.04f, ArmColor);
            DrawCylinder("UpperArmR", j[JointType.ShoulderRight], j[JointType.ElbowRight], 0.05f, ArmColor);
            DrawCylinder("LowerArmR", j[JointType.ElbowRight], j[JointType.WristRight], 0.04f, ArmColor);

            // Legs
            DrawCylinder("UpperLegL", j[JointType.HipLeft], j[JointType.KneeLeft], 0.06f, LegColor);
            DrawCylinder("LowerLegL", j[JointType.KneeLeft], j[JointType.AnkleLeft], 0.05f, LegColor);
            DrawCylinder("FootL", j[JointType.AnkleLeft], j[JointType.FootLeft], 0.045f, LegColor);
            DrawCylinder("UpperLegR", j[JointType.HipRight], j[JointType.KneeRight], 0.06f, LegColor);
            DrawCylinder("LowerLegR", j[JointType.KneeRight], j[JointType.AnkleRight], 0.05f, LegColor);
            DrawCylinder("FootR", j[JointType.AnkleRight], j[JointType.FootRight], 0.045f, LegColor);

            // Volume cues
            DrawSphere("Head", j[JointType.Head], 0.12f, HeadColor, 220);
            DrawSphere("Chest", j[JointType.ShoulderCenter], 0.09f, TorsoColor, 160);
            DrawSphere("Pelvis", j[JointType.HipCenter], 0.085f, TorsoColor, 160);

            // Hands and active-hand highlight
            DrawSphere("HandL", j[JointType.HandLeft], 0.055f, ArmColor, 180);
            DrawSphere("HandR", j[JointType.HandRight], 0.055f, ArmColor, 180);
            DrawSphere("ActiveHand", j[activeHandType], 0.08f, ActiveHandColor, 255);

            // Key joint markers improve depth perception for the avatar.
            DrawSphere("KneeL", j[JointType.KneeLeft], 0.04f, JointColor, 130);
            DrawSphere("KneeR", j[JointType.KneeRight], 0.04f, JointColor, 130);
            DrawSphere("ElbowL", j[JointType.ElbowLeft], 0.038f, JointColor, 130);
            DrawSphere("ElbowR", j[JointType.ElbowRight], 0.038f, JointColor, 130);
        }

        public void Clear()
        {
            foreach (var seg in _segments.Values)
                _viewport.Children.Remove(seg);

            _segments.Clear();
            _smoothedJointPositions.Clear();
        }

        // Private helpers

        private void EnsureSegmentsBuilt()
        {
            var names = new[]
            {
                "ShoulderLine", "Neck", "Spine", "HipLine",
                "UpperArmL", "LowerArmL", "UpperArmR", "LowerArmR",
                "UpperLegL", "LowerLegL", "FootL", "UpperLegR", "LowerLegR", "FootR",
                "Head", "Chest", "Pelvis", "HandL", "HandR", "ActiveHand",
                "KneeL", "KneeR", "ElbowL", "ElbowR"
            };

            foreach (var name in names)
            {
                if (_segments.ContainsKey(name))
                {
                    continue;
                }

                var model = new ModelVisual3D();
                _viewport.Children.Add(model);
                _segments[name] = model;
            }
        }

        private void DrawCylinder(string name, Joint start, Joint end, float radius, Color color)
        {
            if (!_segments.ContainsKey(name))
            {
                return;
            }

            if (!IsRenderable(start) || !IsRenderable(end))
            {
                _segments[name].Content = null;
                return;
            }

            var p1 = GetSmoothedPosition(start.JointType, ToVector3(start.Position));
            var p2 = GetSmoothedPosition(end.JointType, ToVector3(end.Position));
            if ((p2 - p1).Length() < 0.001f)
            {
                return;
            }

            var builder = new MeshBuilder(true, false);
            builder.AddCylinder(p1, p2, radius, 10);

            byte alpha = GetAlpha(start, end, 210);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            var material = new DiffuseMaterial(brush);

            _segments[name].Content = new GeometryModel3D
            {
                Geometry = ToWpfMesh(builder.ToMesh()),
                Material = material,
                BackMaterial = material
            };
        }

        private void DrawSphere(string name, Joint joint, float radius, Color color, byte alpha)
        {
            if (!_segments.ContainsKey(name))
            {
                return;
            }

            if (!IsRenderable(joint))
            {
                _segments[name].Content = null;
                return;
            }

            if (joint.TrackingState == JointTrackingState.Inferred)
            {
                radius *= 0.88f;
                alpha = (byte)Math.Max(70, alpha - 75);
            }

            var center = GetSmoothedPosition(joint.JointType, ToVector3(joint.Position));
            var builder = new MeshBuilder(true, false);
            builder.AddSphere(center, radius, 14, 10);

            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            var material = new DiffuseMaterial(brush);

            _segments[name].Content = new GeometryModel3D
            {
                Geometry = ToWpfMesh(builder.ToMesh()),
                Material = material,
                BackMaterial = material
            };
        }

        private void AddLights()
        {
            _viewport.Children.Add(new ModelVisual3D
            {
                Content = new AmbientLight(Color.FromRgb(120, 120, 120))
            });
            _viewport.Children.Add(new ModelVisual3D
            {
                Content = new DirectionalLight(Colors.White, new Vector3D(0, -1, -1))
            });
        }

        // SkeletonPoint -> System.Numerics.Vector3 (flip X and Z for orientation)
        private static Vector3 ToVector3(SkeletonPoint pos)
            => new Vector3(-pos.X, pos.Y, -pos.Z);

        private static Color ParseColor(string hex)
            => (Color)ColorConverter.ConvertFromString(hex);

        private void HideAllSegments()
        {
            foreach (var segment in _segments.Values)
            {
                segment.Content = null;
            }
        }

        private static bool IsRenderable(Joint joint)
            => joint.TrackingState != JointTrackingState.NotTracked;

        private static byte GetAlpha(Joint a, Joint b, byte trackedAlpha)
        {
            if (a.TrackingState == JointTrackingState.Tracked && b.TrackingState == JointTrackingState.Tracked)
            {
                return trackedAlpha;
            }

            return (byte)Math.Max(80, trackedAlpha - 90);
        }

        private Vector3 GetSmoothedPosition(JointType jointType, Vector3 current)
        {
            if (!_smoothedJointPositions.TryGetValue(jointType, out var previous))
            {
                _smoothedJointPositions[jointType] = current;
                return current;
            }

            var smoothed = Vector3.Lerp(previous, current, PositionSmoothing);
            _smoothedJointPositions[jointType] = smoothed;
            return smoothed;
        }

        private static System.Windows.Media.Media3D.MeshGeometry3D ToWpfMesh(HelixToolkit.Geometry.MeshGeometry3D mesh)
        {
            var wpfMesh = new System.Windows.Media.Media3D.MeshGeometry3D();

            if (mesh.Positions != null)
            {
                foreach (var pos in mesh.Positions)
                {
                    wpfMesh.Positions.Add(new Point3D(pos.X, pos.Y, pos.Z));
                }
            }

            if (mesh.Normals != null)
            {
                foreach (var normal in mesh.Normals)
                {
                    wpfMesh.Normals.Add(new Vector3D(normal.X, normal.Y, normal.Z));
                }
            }

            if (mesh.TriangleIndices != null)
            {
                foreach (var idx in mesh.TriangleIndices)
                {
                    wpfMesh.TriangleIndices.Add(idx);
                }
            }

            return wpfMesh;
        }
    }
}
