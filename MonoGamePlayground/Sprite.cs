using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGamePlayground
{
    public struct SpriteStripe
    {
        public Rectangle ScreenRect { get; set; }
        public Rectangle TextureRect { get; set; }
    }
    public class Sprite
    {
        public Vector2 Position { get; private set; }
        public List<SpriteStripe> Stripes { get; private set; }
        public Rectangle SourceRect { get; private set; }
        public int FrameWidth => SourceRect.Width;
        public int FrameHeight => SourceRect.Height;
        public Texture2D Texture { get; private set; }

        public Sprite(Vector2 position, Texture2D texture)
        {
            Texture = texture;
            SourceRect = new Rectangle(0, 0, 64, 64);
            Position = position;
            Stripes = new List<SpriteStripe>();
        }
    }
}