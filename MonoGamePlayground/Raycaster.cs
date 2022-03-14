using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MonoGamePlayground
{
    internal enum IntersectionType
    {
        NoIntersection,
        IntersectionOnX,
        IntersectionOnY,
        IntersectionOnCorner
    }

    internal struct CollisionInfo
    {
        public IntersectionType Type;
        public float Depth;
        public Vector2 ContactNormal;
    }

    internal struct WallSegmentInfo
    { 
        public int Height { get; set; }
        public bool SideIsEastWestSide { get; set; }
        public double Distance { get; set; }
        public int Texture { get; set; }
        public int TexX { get; set; }
        public int ColumnOnScreenX { get; set; }
    }
    public enum DisplayMode { Default, Precalculations, EqualDistanceSteps, Collisions, MultipleRaysWithCollisions, MultipleRays, MultipleRaysWithFishEyeCorrection, AllOn }
    
    public static class Extensions
    {
        // how many pixel are the size of one map grid unit? pixel to map grid conversion uses this
        public static int UnitSize = 64;    
        public static Vector2 ToScreen(this Vector2 value)
        {
            return value * UnitSize;
        }

        public static string ToPrettyString(this Vector2 value)
        {
            return $"({value.X:#,###.##} / {value.Y:#,###.##})";
        }
    }
    
    public class Raycaster : Game
    {
        private int mMapWidth = 10;
        private int mMapHeight = 10;
        private float mFoVInDegrees = 66.0f;
        private float mWallHeightFactor = 0.8f;
        
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private DebugDrawer mDrawer;
        private DisplayMode mCurrentMode = DisplayMode.Default;

        private KeyboardState mOldKeyboardState;
        
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

        private List<Tuple<Vector2, float>> mCircles = new List<Tuple<Vector2, float>>();
        
        private int[] mMap;
        private Sprite[] mSpriteMap;
        private Vector2 mCameraProjectionPlane;
        public bool ShowEqualDistanceSteps { get; set; }
        public bool ShowCollisionPoints { get; set; }

        public bool ShowPreCalcSteps { get; set; }

        public bool ShowCameraPlane { get; set; }
        public bool ShowGridSteps { get; set; }
        
        public bool TankControls { get; set; }
        
        public bool ShowDebugText { get; set; }
        public bool DoFishEyeCorrection { get; set; }
        public bool DoCollisionsWithMap { get; set; }
        public bool ShowSprites { get; set; }
        public int RayCount { get; set; }
        
        public bool ShowTextures { get; set; }
        
        private int mRayCountNeeded;
        private WallSegmentInfo[] mWallHits;
        private Color[] mColorMap = { Color.Crimson, Color.Coral, Color.Bisque, Color.ForestGreen, Color.DarkOrchid, Color.LightSkyBlue };
        private Rectangle mDestinationOnScreenRect;
        private Rectangle mTextureSegmentRect;
        private Texture2D[] mWallTextures;
        
        private List<Point> mRelativeNeighbors = new List<Point>() { new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1), new Point(1, -1), new Point(-1, 1), new Point(-1, -1), new Point(1, 1) };
        private float mPlayerCollisionRadius = 0.2f;
        private IntersectionType mCurrentIntersection;
        private IntersectionType mLastAxisIntersection;
        private List<Sprite> mVisibleSprites = new List<Sprite>();
        private double[] mZBuffer;
        private Texture2D[] mSpriteTextures;

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
            mWallHits = new WallSegmentInfo[mScreenWidth];
            
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            mPlayerPixelPos = new Vector2(mScreenWidth / 2.0f, mScreenHeight / 2.0f);
            mPlayerPos = mPlayerPixelPos / mUnitSize;
            mPlayerDir = new Vector2(2, 1);
            mPlayerDir.Normalize();

            mZBuffer = new double[mScreenWidth];
            
            mDrawer = new DebugDrawer(_graphics.GraphicsDevice);

            Window.Title = "EduRaycaster";
            Window.AllowUserResizing = false;

            mWallTextures = new[]
            {
                Texture2D.FromFile(_graphics.GraphicsDevice, "Content/Textures/Brick_Wall_64x64.png"),
                Texture2D.FromFile(_graphics.GraphicsDevice, "Content/Textures/bricksx64.png"),
                Texture2D.FromFile(_graphics.GraphicsDevice, "Content/Textures/Green_Wall_Rocks_64x64.png"),
                Texture2D.FromFile(_graphics.GraphicsDevice, "Content/Textures/NIVeR.png"),
                Texture2D.FromFile(_graphics.GraphicsDevice, "Content/Textures/bookshelf.png"),
                Texture2D.FromFile(_graphics.GraphicsDevice, "Content/Textures/felix.png"),
            };
            mSpriteTextures = new[] {Texture2D.FromFile(_graphics.GraphicsDevice, "Content/Textures/spriteOne.png"), Texture2D.FromFile(_graphics.GraphicsDevice, "Content/Textures/SpriteTwo.png"),};
            LoadMap();
        }

        private void LoadMap()
        {
            mSpriteMap = new Sprite[mMapWidth * mMapHeight];
            mMap = new int[mMapWidth * mMapHeight];
            for (int x = 0; x < mMapWidth; x++)
            {
                for (int y = 0; y < mMapHeight; y++)
                {
                    var value = -1;
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

            SetMapAt(9, 9, 0);
            SetMapAt(9, 8, 5);
            SetMapAt(9, 7, 0);
            
            SetSpriteMapAt(4,4, new Sprite(new Vector2(4.5f, 4.5f), mSpriteTextures[0]));
            SetSpriteMapAt(4,6, new Sprite(new Vector2(4.5f, 6.5f), mSpriteTextures[1]));
        }

        private void SetMapAt(int x, int y, int value)
        {
            mMap[y * mMapWidth + x] = value;
        }

        private int GetMapAt(int x, int y)
        {
            if (x < 0 || x >= mMapWidth || y < 0 || y >= mMapHeight)
                return 0;
            return mMap[y * mMapWidth + x];
        }
        
        private void SetSpriteMapAt(int x, int y, Sprite value)
        {
            mSpriteMap[y * mMapWidth + x] = value;
        }

        private Sprite GetSpriteMapAt(int x, int y)
        {
            if (x < 0 || x >= mMapWidth || y < 0 || y >= mMapHeight)
                return null;
            return mSpriteMap[y * mMapWidth + x];
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
                    ShowTextures = false;
                    RayCount = 1;
                    break;
                case DisplayMode.MultipleRays:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    ShowCameraPlane = true;
                    DoFishEyeCorrection = false;
                    ShowTextures = false;
                    RayCount = mRayCountNeeded;
                    break;
                case DisplayMode.MultipleRaysWithFishEyeCorrection:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    ShowCameraPlane = true;
                    DoFishEyeCorrection = true;
                    ShowTextures = false;
                    RayCount = mRayCountNeeded;
                    break;
                case DisplayMode.AllOn:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    ShowCameraPlane = true;
                    DoFishEyeCorrection = true;
                    ShowTextures = true;
                    DoCollisionsWithMap = true;
                    ShowSprites = true;
                    RayCount = mRayCountNeeded;
                    break;
                case DisplayMode.MultipleRaysWithCollisions:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = true;
                    ShowGridSteps = false;
                    ShowCameraPlane = true;
                    DoFishEyeCorrection = false;
                    ShowTextures = false;
                    RayCount = 4;
                    break;
                case DisplayMode.Precalculations:
                    ShowPreCalcSteps = true;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    ShowCameraPlane = false;
                    DoFishEyeCorrection = false;
                    ShowTextures = false;
                    RayCount = 1;
                    break;
                case DisplayMode.EqualDistanceSteps:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = true;
                    ShowCollisionPoints = false;
                    ShowGridSteps = false;
                    ShowCameraPlane = false;
                    DoFishEyeCorrection = false;
                    ShowTextures = false;
                    RayCount = 1;
                    break;
                case DisplayMode.Collisions:
                    ShowPreCalcSteps = false;
                    ShowEqualDistanceSteps = false;
                    ShowCollisionPoints = true;
                    ShowGridSteps = true;
                    ShowCameraPlane = false;
                    DoFishEyeCorrection = false;
                    ShowTextures = false;
                    RayCount = 1;
                    break;
            }

            mWallHits = new WallSegmentInfo[RayCount];
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
            SpriteProjection();

            if (ShowDebugText)
            {
                Display("PlayerPos", mPlayerPos.ToPrettyString());
                Display("PlayerMapPos", mPlayerMapPos.ToString());
                Display("PlayerDir", mPlayerDir.ToPrettyString());
                Display("Intersection", mCurrentIntersection.ToString());
            }
            
            if (DoCollisionsWithMap)
            {
                mCircles.Clear();
                mCircles.Add(new Tuple<Vector2, float>(mPlayerPixelPos, mPlayerCollisionRadius * mUnitSize));
            }
            base.Update(gameTime);
        }

        private void SpriteProjection()
        {
            // Nach Entfernung zum Spieler sortieren
            mVisibleSprites.Sort(
                (spr1, spr2) 
                    => Vector2.DistanceSquared(mPlayerPos, spr2.Position).CompareTo(Vector2.DistanceSquared(mPlayerPos, spr1.Position)));
            
            foreach (var sprite in mVisibleSprites)
            {
                sprite.Stripes.Clear();
                
                mPoints.Add(sprite.Position);
                
                long spriteScreenX = GetSpriteScreenX(sprite.Position, out var spriteDepthZ);

                //calculate height of the sprite on screen
                int spriteHeight = (int)Math.Abs(mScreenHeight / spriteDepthZ); //using "transformY" instead of the real distance prevents fisheye
                
                //calculate lowest and highest pixel to fill in current stripe
                int drawStartY = -spriteHeight / 2 + mScreenHeight / 2;

                int spriteWidth = (int)Math.Abs(mScreenHeight / spriteDepthZ);
                long drawStartX = -spriteWidth / 2 + spriteScreenX;
                if (drawStartX < 0) drawStartX = 0;
                long drawEndX = spriteWidth / 2 + spriteScreenX;
                if (drawEndX >= mScreenWidth) drawEndX = mScreenWidth - 1;
                
                //loop through every vertical stripe of the sprite on screen
                Rectangle sourceRectByIndex = sprite.SourceRect;
                for (long x = drawStartX; x < drawEndX; x++)
                {
                    int texX = (int)((256.0 * (x - (-spriteWidth / 2.0 + spriteScreenX)) * sprite.FrameWidth / spriteWidth) / 256.0);
                    //the conditions in the if are:
                    //1) it's in front of camera plane so you don't see things behind you
                    //2) it's on the screen (left)
                    //3) it's on the screen (right)
                    //4) ZBuffer, with perpendicular distance
                    Rectangle textureSourceRect = new Rectangle(sourceRectByIndex.X + texX, sourceRectByIndex.Y + 0, 1, sprite.FrameHeight);

                    if (spriteDepthZ > 0 && x > 0 && x < mScreenWidth && spriteDepthZ < mZBuffer[x])
                    {
                        Rectangle spriteDestRect = new Rectangle((int)x, drawStartY, 1, spriteHeight);
                        sprite.Stripes.Add(new SpriteStripe() { ScreenRect = spriteDestRect, TextureRect = textureSourceRect });
                    }
                }
            }
        }

        

        /// <summary>
        /// Translates the spritePos by the Camera position, so do not this beforehand.
        /// </summary>
        /// <param name="spritePos">The position of the sprite that will be mapped to the screen.</param>
        /// <param name="transformY">Will contain the depth of the sprite, the Z value.</param>
        /// <returns>The X coordinate on the screen in pixels, where this spritePos should be drawn.</returns>
        private int GetSpriteScreenX(Vector2 spritePos, out double transformY)
        {
            double spriteX = spritePos.X - mPlayerPos.X;
            double spriteY = spritePos.Y - mPlayerPos.Y;

            var dir = mPlayerDir;
            
            double invDet = 1.0 / (mCameraProjectionPlane.X * dir.Y - dir.X * mCameraProjectionPlane.Y);

            double transformX = invDet * (dir.Y * spriteX - dir.X * spriteY);
            transformY = invDet * (-mCameraProjectionPlane.Y * spriteX + mCameraProjectionPlane.X * spriteY);
            
            Display("TransformX", transformX.ToString("0.00"));
            Display("TransformY", transformY.ToString("0.00"));
            
            double relativeX = transformX / transformY;
            Display("relativeX", relativeX.ToString("0.00"));
            // transformX -2..2
            int spriteScreenX = (int)((mScreenWidth / 2.0) * (1 + transformX / transformY));
            //int spriteScreenX = (int) (((transformX + 2.0d) / 4.0d) * mScreenWidth);
            Display("spriteScreenX", spriteScreenX.ToString());
            return spriteScreenX;
        }
        
        private void Raycast()
        {
            mRedLines.Clear();
            mGreenLines.Clear();
            mGreenPoints.Clear();
            mRedPoints.Clear();
            mBlueLines.Clear();
            mPoints.Clear();
            
            mVisibleSprites.Clear();
            
            Vector2 raydir = new Vector2(mPlayerDir.X, mPlayerDir.Y);
            
            for (int columnOnScreen = 0; columnOnScreen < RayCount; columnOnScreen++)
            {
                float cameraX = ((2.0f * columnOnScreen) / (RayCount-1)) - 1.0f; // X-Koordinate in der Kameraebene -1..1
                
                // 1. Richtung der Strahlen festlegen
                if (RayCount > 1)
                {
                    // Vektoraddition und Skalierung:
                    // Richtung in die wir schauen plus anteilige Abweichung nach rechts bzw. links
                    raydir = Vector2.Normalize(mPlayerDir + (mCameraProjectionPlane * cameraX));
                }
                
                // 2. Die sich wiederholenden Strahlenteile für Schritte in X und Y Richtung berechnen
                //    - Steigung des Richtungsvektors und ein Schritt in X bzw. Y Richtung 
                //    - Satz des Pythagoras mit a = 1 und b = ray.y / ray.x für einen Schritt in die X-Richtung
                //    - Satz des Pythagoras mit a = 1 und b = ray.x / ray.y für einen Schritt in die Y-Richtung
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
                
                // 3. Anhand der Richtung des Strahls folgende Werte berechnen
                //     - Die Schrittrichtung auf der Karte (je Quadrant)
                //     - Die Position innerhalb der aktuellen Zelle
                //     - Die anteiligen Startteile der beiden Strahlen für X und Y Schritte
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
                
                // 4. Schrittweise die Strahlen verlängern, immer der kürzeste zuerst
                //    - Die Kartenposition wird aktualisiert
                //    - Die Wandrichtung (Nord/Süd vs. Ost/west) wird gesetzt
                //    - Der Strahl wird verlängert
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
                    
                    // Sprites - Prüfen ob Sprites auf diesem Feld sind
                    Sprite visibleSprite = GetSpriteMapAt(mapX, mapY);
                    if (visibleSprite != null && !mVisibleSprites.Contains(visibleSprite))
                        mVisibleSprites.Add(visibleSprite);
                    
                    // 5. Prüfen wir ob eine Wand getroffen wurde
                    textureIndex = GetMapAt(mapX, mapY);
                    if (textureIndex > -1)
                    {
                        hitWall = true;
                    }
                }
                
                // 6. Den Abstand zur Wand berechnen (Im Prinzip den letzten Schritt rückgängig machen)
                // Vektoren subtrahieren
                double perpWallDist;
                if (eastWestSide)
                {
                    perpWallDist = sideDistX - deltaDistX;
                }
                else
                {
                    perpWallDist = sideDistY - deltaDistY;
                }
                
                // 6.1 Die original Distanz zur Wand für das Texture Mapping verwenden
                double wallX; // X Position an der wir die Wand getroffen haben
                // Komponentenweise Vektoraddition und Skalierung
                if (eastWestSide) wallX = mPlayerPos.Y + (perpWallDist * raydir.Y);
                else wallX = mPlayerPos.X + (perpWallDist * raydir.X);
                wallX -= Math.Floor(wallX); // Uns interessieren nur die Nachkommastellen
                int textureX = (int)(wallX * mWallTextures[textureIndex].Width);
                
                // 6.2 Den korrigierten Abstand zur Wand berechnen
                if (DoFishEyeCorrection)
                {
                    // Kosinus eines Winkels = Ankathete des Winkels / Hypotenuse
                    // Fish-eye correction
                    var rayAngle = ((mFoVInDegrees * 0.5f) * Math.PI) / 180.0f; // between playerDir and rayDir in radians
                    // cameraX(-1) -> : -fov/2
                    perpWallDist *= Math.Cos(rayAngle * cameraX);
                }
                
                mZBuffer[columnOnScreen] = perpWallDist;
                
                // 7. Die Höhe des Wandsegments und beliebige andere Parameter zum Zeichnen ermitteln
                int wallHeight = (int)Math.Abs((mScreenHeight / perpWallDist) * mWallHeightFactor);

                mWallHits[columnOnScreen] = new WallSegmentInfo()
                {
                    Height = wallHeight,
                    SideIsEastWestSide = eastWestSide,
                    Distance = perpWallDist,
                    Texture = textureIndex,
                    TexX = textureX,
                    ColumnOnScreenX = columnOnScreen
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
            
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            
            Draw3DView();
            
            if (ShowDebugText) TextDraw();
            
            _spriteBatch.End();
            
            mDrawer.FlushDrawing();
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

            foreach (var tuple in mCircles)
            {
                mDrawer.DrawCircle(tuple.Item1, tuple.Item2, Color.White);
            }
        }

        private void Draw3DView()
        {
            foreach (var wallHit in mWallHits)
            {
                if (ShowTextures)
                {
                    DrawTextured(wallHit);
                }
                else
                {
                    DrawColored(wallHit);
                }
            }

            if (ShowTextures && ShowSprites)
            {
                DrawSprites();
            }
        }

        private void DrawSprites()
        {
            foreach (var sprite in mVisibleSprites)
            {
                var texture = sprite.Texture;
                foreach (var stripe in sprite.Stripes)
                {
                    var stripeScreenRect = stripe.ScreenRect;
                    stripeScreenRect.X += mScreenWidth;
                    _spriteBatch.Draw(texture, stripeScreenRect, stripe.TextureRect, Color.White, 0f, Vector2.Zero, SpriteEffects.None, 0.9f);
                }
            }
        }

        

        private void DrawColored(WallSegmentInfo wallHit)
        {
            int columnX = wallHit.ColumnOnScreenX;
            var height = wallHit.Height;
            var center = mScreenHeight / 2;
            var baseColor = mColorMap[wallHit.Texture];
            if (wallHit.SideIsEastWestSide)
            {
                baseColor *= 0.7f;
            }

            mDrawer.DrawSegment(
                new Vector2(mScreenWidth + columnX, center - (height / 2)),
                new Vector2(mScreenWidth + columnX, center + (height / 2)),
                baseColor);
        }

        private void DrawTextured(WallSegmentInfo wallHit)
        {
            var texIndex = wallHit.Texture;
            Texture2D texture = mWallTextures[texIndex];
            int texX = wallHit.TexX;
            
            var center = mScreenHeight/2;
            var wallHeight = wallHit.Height;
            var xOnScreen = wallHit.ColumnOnScreenX;
            
            mTextureSegmentRect.X = texX;
            mTextureSegmentRect.Y = 0;
            mTextureSegmentRect.Width = 1;
            mTextureSegmentRect.Height = texture.Height;

            mDestinationOnScreenRect.X = mScreenWidth + xOnScreen;
            mDestinationOnScreenRect.Y = center - (wallHeight/2);
            mDestinationOnScreenRect.Width = 1;
            mDestinationOnScreenRect.Height = wallHeight;
            
            Color drawColor = Color.White;

            if (wallHit.SideIsEastWestSide)
                drawColor = Color.Lerp(drawColor, Color.Black, 0.2f);

            _spriteBatch.Draw(texture, mDestinationOnScreenRect, mTextureSegmentRect, drawColor, 0f, Vector2.Zero, SpriteEffects.None, 0.9f);
        }
        private void DrawGrid()
        {
            for (int x = 0; x < mScreenWidth; x += mUnitSize)
            {
                for (int y = 0; y < mScreenHeight; y += mUnitSize)
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
            Vector2 drawPos = Vector2.Zero;
            var lineHeight = mFont.MeasureString("XMI").Y;
            foreach (var kvp in mDebugInfo)
            {
                string line = kvp.Key + ": " + kvp.Value;                
                _spriteBatch.DrawString(mFont, line, drawPos, Color.Black);
                _spriteBatch.DrawString(mFont, line, drawPos + Vector2.One, Color.White);
                drawPos += Vector2.UnitY * lineHeight;
            }
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
            
            if (keyboardState.IsKeyDown(Keys.N) && mOldKeyboardState.IsKeyUp(Keys.N))
            {
                mFoVInDegrees -= 1.0f;
                Display("FoV", mFoVInDegrees.ToString(CultureInfo.InvariantCulture));
            }
            
            if (keyboardState.IsKeyDown(Keys.M) && mOldKeyboardState.IsKeyUp(Keys.M))
            {
                mFoVInDegrees += 1.0f;
                Display("FoV", mFoVInDegrees.ToString(CultureInfo.InvariantCulture));
            }
            
            
            if (keyboardState.IsKeyDown(Keys.J) && mOldKeyboardState.IsKeyUp(Keys.J))
            {
                mWallHeightFactor -= 0.1f;
                Display("Wall Height Factor", mWallHeightFactor.ToString(CultureInfo.InvariantCulture));
            }
            
            if (keyboardState.IsKeyDown(Keys.K) && mOldKeyboardState.IsKeyUp(Keys.K))
            {
                mWallHeightFactor += 0.1f;
                Display("Wall Height Factor", mWallHeightFactor.ToString(CultureInfo.InvariantCulture));
            }
            
            
            if (keyboardState.IsKeyDown(Keys.R) && mOldKeyboardState.IsKeyUp(Keys.R))
            {
                ShowDebugText = !ShowDebugText;
            }
            
            var moveDirection = Vector2.Zero;
            
            if (keyboardState.IsKeyDown(Keys.A))
            {
                moveDirection -= Vector2.UnitX;
            }

            if (keyboardState.IsKeyDown(Keys.D))
            {
                moveDirection += Vector2.UnitX;
            }

            if (keyboardState.IsKeyDown(Keys.W))
            {
                moveDirection -= Vector2.UnitY;
            }

            if (keyboardState.IsKeyDown(Keys.S))
            {
                moveDirection += Vector2.UnitY;
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
            
            MovePlayer(moveDirection);

            mOldKeyboardState = keyboardState;
        }

        private void MovePlayer(Vector2 direction)
        {
            UpdateCameraPlane();
            
            Vector2 newPixelPosition = TankControls ? mPlayerPixelPos + mPlayerDir * (-direction.Y) : mPlayerPixelPos + direction;

            if (DoCollisionsWithMap)
            {
                newPixelPosition = CollideWithMap(mPlayerPixelPos, newPixelPosition);
            }

            mPlayerPixelPos = newPixelPosition;
            mPlayerPos = newPixelPosition / mUnitSize;
            mPlayerMapPos = new Point((int) mPlayerPos.X, (int) mPlayerPos.Y);
        }

        private Vector2 CollideWithMap(Vector2 currentPosition, Vector2 intendedPosition)
        {
            Vector2 newPos = intendedPosition;
            Vector2 velocity = intendedPosition - currentPosition;
            Display("Velocity", velocity.ToString());
            List<Rectangle> blockingTiles = GetBlockingNeighbors(intendedPosition / mUnitSize);
            mCurrentIntersection = IntersectionType.NoIntersection;
            
            // Kollisionen mit möglichen Nachbarfeldern
            Vector2 correctionVectorWalls = Vector2.Zero;
            Vector2 correctionVectorCorners = Vector2.Zero;
            
            foreach (Rectangle collisionRect in blockingTiles)
            {
                var collisionInfo = Intersects(
                    intendedPosition, 
                    mPlayerCollisionRadius * mUnitSize,
                    collisionRect.Center.ToVector2(),
                    collisionRect.Width,
                    collisionRect.Height);

                var nextIntersection = collisionInfo.Type;
                
                if (nextIntersection == IntersectionType.IntersectionOnX || nextIntersection == IntersectionType.IntersectionOnY)
                {
                    mCurrentIntersection = nextIntersection;
                    correctionVectorWalls += collisionInfo.ContactNormal * collisionInfo.Depth;
                }
                else if (nextIntersection == IntersectionType.IntersectionOnCorner)
                {
                    mCurrentIntersection = nextIntersection;
                    correctionVectorCorners = collisionInfo.ContactNormal * collisionInfo.Depth;
                }
            }
            Vector2 realCorrectionVector;
            
            if (correctionVectorWalls != Vector2.Zero)
            {
                realCorrectionVector = correctionVectorWalls;
            }
            else
            {
                realCorrectionVector = correctionVectorCorners;
            }
            
            newPos = newPos + realCorrectionVector;
            return newPos;
        }

        // Handle intersection between a circle and an axis-aligned rectangle with floating point precision
        private CollisionInfo Intersects(Vector2 circleCenter, float circleRadius, Vector2 rectCenter, float rectWidth, float rectHeight)
        {
            var circleDistanceX = Math.Abs(circleCenter.X - rectCenter.X);  // distance between center of circle and rect on X axis
            var circleDistanceY = Math.Abs(circleCenter.Y - rectCenter.Y);  // distance between center of circle and rect on Y axis

            if (circleDistanceX > (rectWidth / 2 + circleRadius)) { return new CollisionInfo() {Type = IntersectionType.NoIntersection}; }
            if (circleDistanceY > (rectHeight / 2 + circleRadius)) { return new CollisionInfo() {Type = IntersectionType.NoIntersection}; }
            
            var distanceX = (rectWidth / 2) - circleDistanceX;
            var distanceY = (rectHeight / 2) - circleDistanceY;
            
            var NearestX = Math.Clamp(circleCenter.X, rectCenter.X - rectWidth / 2, rectCenter.X + rectWidth / 2);
            var NearestY = Math.Clamp(circleCenter.Y, rectCenter.Y - rectHeight / 2, rectCenter.Y + rectHeight / 2); 
            
            var dist = new Vector2(circleCenter.X - NearestX, circleCenter.Y - NearestY);
            var penetrationDepth = circleRadius - dist.Length();
            
            if (distanceX >= 0)
            {
                var contactNormalY = Vector2.Normalize(new Vector2(0, circleCenter.Y - rectCenter.Y));
                return new CollisionInfo() {
                    Type = IntersectionType.IntersectionOnX,
                    Depth = penetrationDepth,
                    ContactNormal = contactNormalY
                };
            }

            if (distanceY >= 0)
            {
                var contactNormalX = Vector2.Normalize(new Vector2(circleCenter.X - rectCenter.X, 0));
                return new CollisionInfo() {
                    Type = IntersectionType.IntersectionOnY, 
                    Depth = penetrationDepth, 
                    ContactNormal = contactNormalX
                };
            }

            var cornerDistanceSq = 
                Math.Pow(circleDistanceX - rectWidth/2, 2) +
                Math.Pow(circleDistanceY - rectHeight/2, 2);
            

            var cornerCaseIntersection = (cornerDistanceSq <= (circleRadius * circleRadius));
            
            if (cornerCaseIntersection)
            {
                Vector2 contactNormal = Vector2.Zero;
                if (distanceX > distanceY)
                {
                    contactNormal = Vector2.Normalize(new Vector2(0, circleCenter.Y - rectCenter.Y));
                }
                else
                {
                    contactNormal = Vector2.Normalize(new Vector2(circleCenter.X - rectCenter.X, 0));
                }
                
                return new CollisionInfo()
                {
                    Type = IntersectionType.IntersectionOnCorner,
                    Depth = penetrationDepth,
                    ContactNormal = contactNormal
                };
            }
            else
            { return new CollisionInfo() {Type = IntersectionType.NoIntersection,}; }
        }
        
        private List<Rectangle> GetBlockingNeighbors(Vector2 position)
        {
            List<Rectangle> neighborTiles = new List<Rectangle>();
            Rectangle startTile = new Rectangle((int)Math.Floor(position.X) * mUnitSize, (int)Math.Floor(position.Y) * mUnitSize, mUnitSize, mUnitSize);

            foreach (Point neighborPos in mRelativeNeighbors)
            {
                var neighborPosX = startTile.X + (neighborPos.X * mUnitSize);
                var neighborPosY = startTile.Y + (neighborPos.Y * mUnitSize);
                
                var neighborTileMapX = neighborPosX / mUnitSize;
                var neighborTileMapY = neighborPosY / mUnitSize;

                if (neighborTileMapX > mMapWidth - 1 || neighborTileMapX < 0 || neighborTileMapY > mMapHeight - 1 ||
                    neighborTileMapY < 0)
                {
                    neighborTiles.Add(new Rectangle(neighborPosX, neighborPosY, mUnitSize, mUnitSize));
                    continue;
                }

                int metaData = GetMapAt(neighborTileMapX, neighborTileMapY);
                if (metaData > -1) // is a wall
                {
                    neighborTiles.Add(new Rectangle(neighborPosX, neighborPosY, mUnitSize, mUnitSize));
                }
            }
            return neighborTiles;
        }

        private void UpdateCameraPlane()
        {
            float camwidth = (float)Math.Tan(MathHelper.ToRadians(mFoVInDegrees) / 2);
            mCameraProjectionPlane = Vector2.Normalize(new Vector2(-mPlayerDir.Y, mPlayerDir.X)) * camwidth;
        }
    }

    
}
