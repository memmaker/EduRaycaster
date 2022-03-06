using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Math = System.Math;

namespace MonoGamePlayground
{
    public class DebugDrawer
    {
        public const int MAX_VERTS = 4000;
        public const int MAX_INDICES = 4000;

        BasicEffect basicEffect;
        DynamicVertexBuffer vertexBuffer;
        DynamicIndexBuffer indexBuffer;

        ushort[] Indices = new ushort[MAX_INDICES];
        VertexPositionColor[] Vertices = new VertexPositionColor[MAX_VERTS];
        int IndexCount;
        int VertexCount;
        
        public DebugDrawer(GraphicsDevice device)
        {
            vertexBuffer = new DynamicVertexBuffer(device, typeof(VertexPositionColor), MAX_VERTS, BufferUsage.WriteOnly);
            indexBuffer = new DynamicIndexBuffer(device, typeof(ushort), MAX_INDICES, BufferUsage.WriteOnly);
            
            basicEffect = new BasicEffect(device); //(device, null);
            basicEffect.LightingEnabled = false;
            basicEffect.VertexColorEnabled = true;
            basicEffect.TextureEnabled = false;
            Begin(device);
        }
        public void Begin(GraphicsDevice device)
        {
            Matrix projection =
                Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, -1);
            Matrix halfPixelOffset = Matrix.CreateTranslation(-0.5f, -0.5f, 0);
            basicEffect.World = Matrix.Identity;
            basicEffect.View = Matrix.Identity;
            basicEffect.Projection = halfPixelOffset * projection;

            VertexCount = 0;
            IndexCount = 0;
        }
        public void DrawPolygon(Vector2[] vertices, int vertexCount, Color color)
        {
            for (int i = 0; i < vertexCount - 1; i++)
            {
                DrawSegment(vertices[i], vertices[i+1], color);
            }
            DrawSegment(vertices[vertexCount - 1], vertices[0], color);
        }
        
        public void DrawCircle(Vector2 center, float radius, Color color)
        {
            int pointCount = (int) Math.Ceiling((radius)* Math.PI);
            Vector2[] vertices = new Vector2[pointCount];
            
            var pointTheta = ((float)Math.PI * 2) / (vertices.Length - 1);
            for (int i = 0; i < vertices.Length; i++)
            {
                var theta = pointTheta * i;
                var x = center.X + ((float)Math.Sin(theta) * radius);
                var y = center.Y + ((float)Math.Cos(theta) * radius);
                vertices[i] = new Vector2(x, y);
            }
            DrawPolygon(vertices, pointCount, color);
        }
        public void DrawSegment(Vector2 p1, Vector2 p2, Color color)
        {
            if(Reserve(2, 2))
            {
                Indices[IndexCount++] = (ushort)VertexCount;
                Indices[IndexCount++] = (ushort)(VertexCount+1);
                Vertices[VertexCount++] = new VertexPositionColor(new Vector3(p1.X, p1.Y, 0), color);
                Vertices[VertexCount++] = new VertexPositionColor(new Vector3(p2.X, p2.Y, 0), color);
            }
        }

        public void DrawPoint(Vector2 point, Color color)
        {
            float offset = 4.0f;
            DrawSegment(
                new Vector2(point.X - offset, point.Y - offset),
                new Vector2(point.X + offset, point.Y + offset), 
                color);
            DrawSegment(
                new Vector2(point.X + offset, point.Y - offset),
                new Vector2(point.X - offset, point.Y + offset), 
                color);
        }
        
        // Check if there's enough space to draw an object with the given vertex/index counts.
        // If necessary, call FlushDrawing() to make room.
        private bool Reserve(int numVerts, int numIndices)
        {
            if(numVerts > MAX_VERTS || numIndices > MAX_INDICES)
            {
                // Whatever it is, we can't draw it
                return false;
            }
            if (VertexCount + numVerts > MAX_VERTS || IndexCount + numIndices >= MAX_INDICES)
            {
                // We can draw it, but we need to make room first
                FlushDrawing();
            }
            return true;
        }
        
        // Draw any queued objects and reset our line buffers
        public void FlushDrawing()
        {
            if (IndexCount > 0)
            {
                vertexBuffer.SetData(Vertices, 0, VertexCount, SetDataOptions.Discard);
                indexBuffer.SetData(Indices, 0, IndexCount, SetDataOptions.Discard);

                GraphicsDevice device = basicEffect.GraphicsDevice;
                device.SetVertexBuffer(vertexBuffer);
                device.Indices = indexBuffer;

                foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawIndexedPrimitives(PrimitiveType.LineList,  0, 0, IndexCount / 2);
                }

                device.SetVertexBuffer(null);
                device.Indices = null;
            }
            IndexCount = 0;
            VertexCount = 0;
        }
    }
}