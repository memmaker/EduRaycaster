using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoGamePlayground
{
    public enum DisplayMode { Default, Precalculations, EqualDistanceSteps, Collisions }
    public static class Extensions
    {
        public static int UnitSize = 32;
        public static Vector2 ToScreen(this Vector2 value)
        {
            return value * UnitSize;
        }
    }
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private DebugDrawer mDrawer;
        private DisplayMode mCurrentMode = DisplayMode.Default;

        private KeyboardState mOldKeyboardState;
        
        private int mMapWidth = 20;
        private int mMapHeight = 20;
        private int mScreenWidth;
        private int mScreenHeight;
        private Vector2 mPlayerPixelPos;
        private Vector2 mPlayerDir;
        private int mUnitSize;
        private SpriteFont mFont;
        private Dictionary<string, string> mDebugInfo = new Dictionary<string, string>();
        private Point mPlayerMapPos;
        private Vector2 mPlayerPos;
        private Vector2 mStartVector;
        private Vector2 mFirstCollisionX;
        private List<Tuple<Vector2,Vector2>> mRedLines = new List<Tuple<Vector2, Vector2>>();
        private List<Tuple<Vector2,Vector2>> mGreenLines = new List<Tuple<Vector2, Vector2>>();
        
        private List<Tuple<Vector2,Vector2>> mBlueLines = new List<Tuple<Vector2, Vector2>>();
        private List<Vector2> mPoints = new List<Vector2>();
        
        private List<Vector2> mGreenPoints = new List<Vector2>();
        private List<Vector2> mRedPoints = new List<Vector2>();
        
        private int[] mMap;
        public bool ShowEqualDistanceSteps { get; set; }
        public bool ShowCollisionPoints { get; set; }

        public bool ShowPreCalcSteps { get; set; }

        public bool ShowGridSteps { get; set; }
        public Game1()
        {
            mUnitSize = Extensions.UnitSize;
            mScreenWidth = mMapWidth * mUnitSize;
            mScreenHeight = mMapHeight * mUnitSize;
            
            _graphics = new GraphicsDeviceManager(this);
            _graphics.ApplyChanges();
            _graphics.PreferredBackBufferWidth = mScreenWidth; // 2:1
            _graphics.PreferredBackBufferHeight = mScreenHeight;
            _graphics.ApplyChanges();

            LoadMap();

            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            mPlayerPixelPos = new Vector2(mScreenWidth / 2.0f, mScreenHeight / 2.0f);
            mPlayerDir = new Vector2(2, 1);
            mPlayerDir.Normalize();
            
            mDrawer = new DebugDrawer(_graphics.GraphicsDevice);
        }

        private void LoadMap()
        {
            mMap = new int[mMapWidth * mMapHeight];
            for (int x = 0; x < mMapWidth; x++)
            {
                for (int y = 0; y < mMapHeight; y++)
                {
                    var value = 0;
                    if (x == 0 || y == 0 || x == mMapWidth - 1 || y == mMapHeight - 1)
                    {
                        value = 1;
                    }

                    SetMapAt(x, y, value);
                }
            }

            SetMapAt(14, 1, 1);
            SetMapAt(14, 2, 1);
            SetMapAt(14, 3, 1);
            SetMapAt(13, 3, 1);
            SetMapAt(12, 3, 1);
        }

        private void NextDisplayMode()
        {
            mCurrentMode = (DisplayMode)(((int)mCurrentMode + 1) % Enum.GetNames(typeof(DisplayMode)).Length);
            switch (mCurrentMode)
            {
                case DisplayMode.Default:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    break;
                case DisplayMode.Precalculations:
                    ShowPreCalcSteps = true;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    break;
                case DisplayMode.EqualDistanceSteps:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = true;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    break;
                case DisplayMode.Collisions:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = true;
                    ShowGridSteps = true;
                    break;
            }
        }

        private void SetMapAt(int x, int y, int value)
        {
            mMap[y * mMapWidth + x] = value;
        }

        private int GetMapAt(int x, int y)
        {
            if (x < 0 || x >= mMapWidth || y < 0 || y >= mMapHeight)
                return 1;
            return mMap[y * mMapWidth + x];
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            mFont = Content.Load<SpriteFont>("Fonts/Default");
        }

        protected override void Update(GameTime gameTime)
        {
            HandleInput();

            Raycast();
            
            Display("PlayerPos", mPlayerPos.ToString());
            Display("PlayerMapPos", mPlayerMapPos.ToString());
            Display("PlayerDir", mPlayerDir.ToString());
            
            base.Update(gameTime);
        }

        private void Raycast()
        {
            mRedLines.Clear();
            mGreenLines.Clear();
            mGreenPoints.Clear();
            mRedPoints.Clear();
            mBlueLines.Clear();
            mPoints.Clear();
            
            Vector2 raydir = new Vector2(mPlayerDir.X, mPlayerDir.Y);

            float deltaDistX = (float) Math.Sqrt(1 + (raydir.Y * raydir.Y) / (raydir.X * raydir.X));
            float deltaDistY = (float) Math.Sqrt(1 + (raydir.X * raydir.X) / (raydir.Y * raydir.Y));
            
            //float deltaDistX = Math.Abs(raydir.X / raydir.Y);
            //float deltaDistY = Math.Abs(raydir.Y / raydir.X);

            int mapY = mPlayerMapPos.Y;
            int mapX = mPlayerMapPos.X;
            
            float intraCellPositionY;
            float intraCellPositionX;
            
            float sideDistX;
            float sideDistY;

            int mapStepX;
            int mapStepY;

            int mapDrawOffSetX = 0;
            int mapDrawOffSetY = 0;

            if (raydir.X < 0)
            {
                //sideDistX *= -1;
                mapStepX = -1;
                intraCellPositionX = mPlayerPos.X - mapX;
                sideDistX = intraCellPositionX * deltaDistX;
            }
            else
            {
                mapStepX = 1;
                //intraCellPositionX = 1 - (mPlayerPos.X - mPlayerMapPos.X);
                intraCellPositionX = (mapX + 1.0f - mPlayerPos.X);
                mapDrawOffSetX = 1;
                sideDistX = intraCellPositionX * deltaDistX;
            }
            
            
            if (raydir.Y < 0)
            {
                //deltaDistY *= -1;
                mapStepY = -1;
                intraCellPositionY = mPlayerPos.Y - mapY;
                sideDistY = intraCellPositionY * deltaDistY;
            }
            else
            {
                mapStepY = 1;
                intraCellPositionY = (mapY + 1.0f - mPlayerPos.Y);
                sideDistY = intraCellPositionY * deltaDistY;
            }

            if (ShowPreCalcSteps)
            {
                mRedPoints.Add(mPlayerPos + (raydir * sideDistX));
                mGreenPoints.Add(mPlayerPos + (raydir * sideDistY));
            }
            //DrawSteps(raydir, sideDistX, sideDistY, mapX + mapDrawOffSetX, mapY + mapDrawOffSetY);

            bool northSouthSide;
            bool hitWall = false;
            while (!hitWall)
            {
                Vector2 nextCollision;
                if (sideDistX < sideDistY)
                {
                    // move in Y Direction
                    nextCollision = mPlayerPos + (raydir * sideDistX);
                    if (ShowCollisionPoints) mRedPoints.Add(nextCollision);
                    if (ShowEqualDistanceSteps)
                    {
                        mRedLines.Add(new Tuple<Vector2, Vector2>(
                        nextCollision,
                        new Vector2(mPlayerPos.X, nextCollision.Y)));
                        
                    }
                    
                    
                    mapX += mapStepX;
                    northSouthSide = true;
                    sideDistX += deltaDistX;
                }
                else
                {
                    // move in X Direction
                    nextCollision = mPlayerPos + (raydir * sideDistY);
                    if (ShowCollisionPoints) mGreenPoints.Add(nextCollision);
                    if (ShowEqualDistanceSteps)
                    {
                        mGreenLines.Add(new Tuple<Vector2, Vector2>(
                            nextCollision,
                            new Vector2(nextCollision.X, mPlayerPos.Y)));
                    }

                    mapY += mapStepY;
                    northSouthSide = false;
                    sideDistY += deltaDistY;
                }
                
                if (ShowGridSteps) mPoints.Add(new Vector2(mapX + 0.5f, mapY + 0.5f));
                
                if (GetMapAt(mapX, mapY) > 0)
                {
                    hitWall = true;
                    mBlueLines.Add(new Tuple<Vector2, Vector2>(
                        nextCollision,
                        mPlayerPos));
                }
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // Draw Grid
            DrawGrid();
            
            // Player
            mDrawer.DrawPoint(mPlayerPos.ToScreen(), Color.White);
            
            foreach (var line in mRedLines)
            {
                mDrawer.DrawSegment(line.Item1.ToScreen(), line.Item2.ToScreen(), Color.Red);
            }
            
            foreach (var line in mGreenLines)
            {
                mDrawer.DrawSegment(line.Item1.ToScreen(), line.Item2.ToScreen(), Color.LightGreen);
            }
            
            foreach (var line in mBlueLines)
            {
                mDrawer.DrawSegment(line.Item1.ToScreen(), line.Item2.ToScreen(), Color.LightBlue);
            }
            
            foreach (var point in mPoints)
            {
                mDrawer.DrawPoint(point.ToScreen(), Color.White);
            }
            
            foreach (var point in mRedPoints)
            {
                mDrawer.DrawPoint(point.ToScreen(), Color.Red);
            }
            
            foreach (var point in mGreenPoints)
            {
                mDrawer.DrawPoint(point.ToScreen(), Color.LightGreen);
            }
            
            mDrawer.FlushDrawing();
            
            TextDraw();
            
            base.Draw(gameTime);
        }

        private void DrawGrid()
        {
            for (int x = 0; x < 640; x += mUnitSize)
            {
                for (int y = 0; y < 640; y += mUnitSize)
                {
                    mDrawer.DrawSegment(new Vector2(x, 0), new Vector2(x, mScreenHeight), Color.Gray);
                    mDrawer.DrawSegment(new Vector2(0, y), new Vector2(mScreenWidth, y), Color.Gray);

                    int xGrid = x / mUnitSize;
                    int yGrid = y / mUnitSize;
                    if (GetMapAt(xGrid, yGrid) > 0)
                    {
                        float pointX = xGrid + 0.5f;
                        float pointY = yGrid + 0.5f;
                        mDrawer.DrawPoint(new Vector2(pointX, pointY).ToScreen(), Color.Aquamarine);
                    }
                }
            }
        }

        private void Display(string key, string text)
        {
            if (mDebugInfo.ContainsKey(key))
            {
                mDebugInfo[key] = text;
            }
            else
            {
                mDebugInfo.Add(key, text);
            }
        }
        private void TextDraw()
        {
            _spriteBatch.Begin();
            Vector2 drawPos = Vector2.Zero;
            var lineHeight = mFont.MeasureString("XMI").Y;
            foreach (var kvp in mDebugInfo)
            {
                string line = kvp.Key + ": " + kvp.Value;                
                _spriteBatch.DrawString(mFont, line, drawPos, Color.Black);
                _spriteBatch.DrawString(mFont, line, drawPos + Vector2.One, Color.White);
                drawPos += Vector2.UnitY * lineHeight;
            }
            _spriteBatch.End();
        }
        
        private void HandleInput()
        {
            var keyboardState = Keyboard.GetState();
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboardState.IsKeyDown(Keys.Escape))
                Exit();
            
            
            if (keyboardState.IsKeyDown(Keys.Tab) && mOldKeyboardState.IsKeyUp(Keys.Tab))
            {
                NextDisplayMode();
            }
            
            var move = Vector2.Zero;
            if (keyboardState.IsKeyDown(Keys.A))
            {
                move -= Vector2.UnitX;
            }

            if (keyboardState.IsKeyDown(Keys.D))
            {
                move += Vector2.UnitX;
            }

            if (keyboardState.IsKeyDown(Keys.W))
            {
                move -= Vector2.UnitY;
            }

            if (keyboardState.IsKeyDown(Keys.S))
            {
                move += Vector2.UnitY;
            }

            float rotateAmount = 2f;
            Matrix rotationMatrix = Matrix.Identity;

            if (keyboardState.IsKeyDown(Keys.Q))
            {
                rotationMatrix = Matrix.CreateRotationZ(MathHelper.ToRadians(-rotateAmount));
            }
            else if (keyboardState.IsKeyDown(Keys.E))
            {
                rotationMatrix = Matrix.CreateRotationZ(MathHelper.ToRadians(rotateAmount));
            }

            mPlayerDir = Vector2.Transform(mPlayerDir, rotationMatrix);
            mPlayerPixelPos += move;
            mPlayerMapPos = new Point((int) mPlayerPixelPos.X / mUnitSize, (int) mPlayerPixelPos.Y / mUnitSize);
            
            mPlayerPos = new Vector2(
                mPlayerPixelPos.X / mUnitSize,
                mPlayerPixelPos.Y / mUnitSize);

            mOldKeyboardState = keyboardState;
        }

    }
}
