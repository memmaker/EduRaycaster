using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoGamePlayground
{
    public enum DisplayMode { Default, Precalculations, EqualDistanceSteps, Collisions, MultipleRaysWithCollisions, MultipleRays, MultipleRaysWithFishEyeCorrection }
    public static class Extensions
    {
        public static int UnitSize = 64;
        public static Vector2 ToScreen(this Vector2 value)
        {
            return value * UnitSize;
        }
    }
    public class Raycaster : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private DebugDrawer mDrawer;
        private DisplayMode mCurrentMode = DisplayMode.Default;

        private KeyboardState mOldKeyboardState;
        
        private int mMapWidth = 10;
        private int mMapHeight = 10;
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
        private Vector2 mCameraProjectionPlane;
        public bool ShowEqualDistanceSteps { get; set; }
        public bool ShowCollisionPoints { get; set; }

        public bool ShowPreCalcSteps { get; set; }

        public bool ShowCameraPlane { get; set; }
        public bool ShowGridSteps { get; set; }
        
        public bool TankControls { get; set; }
        
        public bool DoFishEyeCorrection { get; set; }
        public int RayCount { get; set; }
        private int mRayCountNeeded;
        private Wallhit[] mWallHits;
        private float mFov = 66.0f;
        private Color[] mColorMap = new[] { Color.Crimson, Color.Coral, Color.Bisque, Color.ForestGreen };

        public Raycaster()
        {
            RayCount = 1;
            mUnitSize = Extensions.UnitSize;
            mScreenWidth = mMapWidth * mUnitSize;
            mScreenHeight = mMapHeight * mUnitSize;
            
            _graphics = new GraphicsDeviceManager(this);
            _graphics.ApplyChanges();
            _graphics.PreferredBackBufferWidth = mScreenWidth * 2; // 2:1
            _graphics.PreferredBackBufferHeight = mScreenHeight;
            _graphics.ApplyChanges();

            mRayCountNeeded = mScreenWidth;
            mWallHits = new Wallhit[mScreenWidth];
            
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
                    var value = -1;
                    /*if (x == 0 || y == 0 || x == mMapWidth - 1 || y == mMapHeight - 1)
                    {
                        value = 0;
                    }
                    */
                    SetMapAt(x, y, value);
                }
            }

            SetMapAt(4, 1, 1);
            SetMapAt(4, 2, 2);
            SetMapAt(4, 3, 0);
            SetMapAt(3, 3, 0);
            SetMapAt(2, 3, 3);
            
            SetMapAt(5, 6, 1);
            SetMapAt(6, 7, 2);
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
                    ShowCameraPlane = false;
                    DoFishEyeCorrection = false;
                    RayCount = 1;
                    break;
                case DisplayMode.MultipleRays:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    ShowCameraPlane = true;
                    DoFishEyeCorrection = false;
                    RayCount = mRayCountNeeded;
                    break;
                case DisplayMode.MultipleRaysWithFishEyeCorrection:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    ShowCameraPlane = true;
                    DoFishEyeCorrection = true;
                    RayCount = mRayCountNeeded;
                    break;
                case DisplayMode.MultipleRaysWithCollisions:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = true;
                    ShowGridSteps = false;
                    ShowCameraPlane = true;
                    DoFishEyeCorrection = false;
                    RayCount = 4;
                    break;
                case DisplayMode.Precalculations:
                    ShowPreCalcSteps = true;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    ShowCameraPlane = false;
                    DoFishEyeCorrection = false;
                    RayCount = 1;
                    break;
                case DisplayMode.EqualDistanceSteps:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = true;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    ShowCameraPlane = false;
                    DoFishEyeCorrection = false;
                    RayCount = 1;
                    break;
                case DisplayMode.Collisions:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = true;
                    ShowGridSteps = true;
                    ShowCameraPlane = false;
                    DoFishEyeCorrection = false;
                    RayCount = 1;
                    break;
            }

            mWallHits = new Wallhit[RayCount];
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

            for (int columnOnScreen = 0; columnOnScreen < RayCount; columnOnScreen++)
            {
                float cameraX = ((2.0f * columnOnScreen) / (RayCount-1)) - 1.0f; // x-coordinate in camera space -1..1
                
                if (RayCount > 1)
                {
                    raydir = Vector2.Normalize(mPlayerDir + (mCameraProjectionPlane * cameraX));
                }

                float deltaDistX = (float) Math.Sqrt(1 + (raydir.Y * raydir.Y) / (raydir.X * raydir.X));
                float deltaDistY = (float) Math.Sqrt(1 + (raydir.X * raydir.X) / (raydir.Y * raydir.Y));
                
                int mapX = mPlayerMapPos.X;
                int mapY = mPlayerMapPos.Y;
                
                float intraCellPositionX;
                float intraCellPositionY;

                float sideDistX;
                float sideDistY;

                int mapStepX;
                int mapStepY;

                if (raydir.X < 0)
                {
                    mapStepX = -1;
                    intraCellPositionX = mPlayerPos.X - mapX;
                    sideDistX = intraCellPositionX * deltaDistX;
                }
                else
                {
                    mapStepX = 1;
                    intraCellPositionX = (mapX + 1.0f - mPlayerPos.X);
                    sideDistX = intraCellPositionX * deltaDistX;
                }

                if (raydir.Y < 0)
                {
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

                if (ShowPreCalcSteps) DrawPreCalcSteps(raydir, sideDistX, sideDistY);
                
                bool eastWestSide = false;
                bool hitWall = false;
                Vector2 nextCollision = Vector2.Zero;
                int textureIndex = -1;
                
                while (!hitWall)
                {
                    if (sideDistX < sideDistY)
                    {
                        // move one unit in X Direction
                        nextCollision = mPlayerPos + (raydir * sideDistX);
                        if (ShowCollisionPoints) mRedPoints.Add(nextCollision);
                        if (ShowEqualDistanceSteps)
                        {
                            mRedLines.Add(new Tuple<Vector2, Vector2>(
                                nextCollision,
                                new Vector2(mPlayerPos.X, nextCollision.Y)));

                        }

                        mapX += mapStepX;
                        eastWestSide = true;
                        sideDistX += deltaDistX;
                    }
                    else
                    {
                        // move one unit in Y Direction
                        nextCollision = mPlayerPos + (raydir * sideDistY);
                        if (ShowCollisionPoints) mGreenPoints.Add(nextCollision);
                        if (ShowEqualDistanceSteps)
                        {
                            mGreenLines.Add(new Tuple<Vector2, Vector2>(
                                nextCollision,
                                new Vector2(nextCollision.X, mPlayerPos.Y)));
                        }

                        mapY += mapStepY;
                        eastWestSide = false;
                        sideDistY += deltaDistY;
                    }

                    if (ShowGridSteps) mPoints.Add(new Vector2(mapX + 0.5f, mapY + 0.5f));

                    textureIndex = GetMapAt(mapX, mapY);
                    if (textureIndex > -1)
                    {
                        hitWall = true;
                    }
                }

                double perpWallDist;
                if (eastWestSide)
                {
                    perpWallDist = sideDistX - deltaDistX;
                }
                else
                {
                    perpWallDist = sideDistY - deltaDistY;
                }

                if (DoFishEyeCorrection)
                {
                    // Fish-eye correction
                    var rayAngle = ((mFov * 0.5f) * Math.PI) / 180.0f; // between playerDir and rayDir in radians
                    // cameraX(-1) -> : -fov/2
                    perpWallDist *= Math.Cos(rayAngle * cameraX);
                }

                int lineHeight = (int)Math.Abs(mScreenHeight / perpWallDist);
                mWallHits[columnOnScreen] = new Wallhit()
                {
                    Height = lineHeight,
                    SideIsEastWestSide = eastWestSide,
                    Distance = perpWallDist,
                    Texture = textureIndex
                };
                
                // lastcollision = end of the ray
                if (ShowCameraPlane)
                {
                    mBlueLines.Add(new Tuple<Vector2, Vector2>(
                        mPlayerPos + (mCameraProjectionPlane * -1.0f),
                        mPlayerPos + (mCameraProjectionPlane * 1.0f)
                    ));
                }
                
                // The ray itself
                mBlueLines.Add(new Tuple<Vector2, Vector2>(
                    mPlayerPos,
                    nextCollision
                ));
            }
        }

        private void DrawPreCalcSteps(Vector2 raydir, float sideDistX, float sideDistY)
        {
            var firstCollisionX = mPlayerPos + (raydir * sideDistX);
            mRedPoints.Add(firstCollisionX);
            mRedLines.Add(new Tuple<Vector2, Vector2>(
                mPlayerPos,
                new Vector2(firstCollisionX.X, mPlayerPos.Y)
            ));
            mRedLines.Add(new Tuple<Vector2, Vector2>(
                firstCollisionX,
                new Vector2(firstCollisionX.X, mPlayerPos.Y)
            ));

            var firstCollisionY = mPlayerPos + (raydir * sideDistY);
            mGreenPoints.Add(firstCollisionY);
            mGreenLines.Add(new Tuple<Vector2, Vector2>(
                mPlayerPos,
                new Vector2(mPlayerPos.X, firstCollisionY.Y)
            ));
            mGreenLines.Add(new Tuple<Vector2, Vector2>(
                firstCollisionY,
                new Vector2(mPlayerPos.X, firstCollisionY.Y)
            ));
        }
        
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            Draw2DView();

            Draw3DView();
            
            mDrawer.FlushDrawing();
            
            TextDraw();
            
            base.Draw(gameTime);
        }

        private void Draw2DView()
        {
            // Draw Grid
            DrawGrid();

            // Player
            mDrawer.DrawPoint(mPlayerPos.ToScreen(), Color.White);

            DrawColoredLinesAndPoints();
        }

        private void DrawColoredLinesAndPoints()
        {
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
        }

        private void Draw3DView()
        {
            for (var columnX = 0; columnX < mWallHits.Length; columnX++)
            {
                var wallHit = mWallHits[columnX];
                var height = wallHit.Height;
                var center = mScreenHeight/2;
                var baseColor = mColorMap[wallHit.Texture];
                if (wallHit.SideIsEastWestSide)
                {
                    baseColor *= 0.7f;
                }
                mDrawer.DrawSegment(
                    new Vector2(mScreenWidth + columnX, center - (height/2)),
                    new Vector2(mScreenWidth + columnX, center + (height/2)),
                    baseColor);
            }
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
                    var textureIndex = GetMapAt(xGrid, yGrid);
                    if (textureIndex > -1)
                    {
                        float minX = xGrid;
                        float minY = yGrid;
                        float maxX = xGrid + 1.0f;
                        float maxY = yGrid + 1.0f;
                        for (float yOffset = minY; yOffset <= maxY; yOffset += 0.1f)
                        {
                            mDrawer.DrawSegment(new Vector2(minX, yOffset).ToScreen(),
                                new Vector2(maxX, yOffset).ToScreen(), mColorMap[textureIndex]);
                        }
                        
                        for (float xOffset = minX; xOffset <= maxX; xOffset += 0.1f)
                        {
                            mDrawer.DrawSegment(new Vector2(xOffset, minY).ToScreen(),
                                new Vector2(xOffset, maxY).ToScreen(), mColorMap[textureIndex]);
                        }

                        //mDrawer.DrawPoint(new Vector2(pointX, pointY).ToScreen(), mColorMap[textureIndex]);
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
            
            if (keyboardState.IsKeyDown(Keys.T) && mOldKeyboardState.IsKeyUp(Keys.T))
            {
                TankControls = !TankControls;
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
            UpdateCameraPlane();

            if (TankControls)
            {
                mPlayerPixelPos += mPlayerDir * (-move.Y);
            }
            else
            {
                mPlayerPixelPos += move;
            }
            
            mPlayerMapPos = new Point((int) mPlayerPixelPos.X / mUnitSize, (int) mPlayerPixelPos.Y / mUnitSize);
            
            mPlayerPos = new Vector2(
                mPlayerPixelPos.X / mUnitSize,
                mPlayerPixelPos.Y / mUnitSize);

            mOldKeyboardState = keyboardState;
        }
        
        private void UpdateCameraPlane()
        {
            float camwidth = (float)Math.Tan(MathHelper.ToRadians(mFov) / 2);
            mCameraProjectionPlane = Vector2.Normalize(new Vector2(-mPlayerDir.Y, mPlayerDir.X)) * camwidth;
        }
    }

    internal struct Wallhit
    {
        public int Height { get; set; }
        public bool SideIsEastWestSide { get; set; }
        public double Distance { get; set; }
        public int Texture { get; set; }
    }
}
