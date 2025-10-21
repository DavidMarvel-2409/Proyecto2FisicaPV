/*
 Codigo creado por:
        -David Valdés Hernández
        -Pedro Morales Méndez
        -Daniel Muñoz Muñoz
 */

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace ProyectoUnidad2DaValDaMuPeMo
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _gdm;
        private BasicEffect _effect;

        // Buffers de la esfera
        private VertexBuffer _vb;
        private IndexBuffer _ib;
        private int _primitiveCount; // triángulos totales

        // Parámetros de malla (ligados a la presentación)
        private float _radius = 0.25f;
        private int _stacks = 18; // φ (0..π)
        private int _slices = 36; // θ (0..2π)

        // Transformaciones
        private Matrix _world, _view, _proj;
        private float _angle;     // animación de rotación
        private bool _rotate = true;

        // Render states
        private RasterizerState _rsSolid;
        private RasterizerState _rsWire;
        private bool _wireframe = false;

        Random rnd = new Random();
        private float worldLimits = /*1.2f*/(16 / 9) * 1.4f;
        private Vector3 _pos;
        private Vector3 _vel;
        private Vector3 _accel;
        private float _g = -9.8f;

        //Controles
        Keys LEFT = Keys.A, RIGHT = Keys.D, FRONT = Keys.W, BACK = Keys.S, JUMP = Keys.Space, EXIT = Keys.Escape, WINDOW_PLUS = Keys.Up, WINDOW_MINUS = Keys.Down;
        int _H;

        public Game1()
        {
            _gdm = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // Cámara mirando al origen desde +Z
            float fov = MathHelper.ToRadians(45f);
            float aspect = GraphicsDevice.Viewport.AspectRatio;

            _view = Matrix.CreateLookAt(new Vector3(0, 0, 4.5f), Vector3.Zero, Vector3.Up);
            _proj = Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 100f);
            _world = Matrix.Identity;

            // Rasterizer (para alternar sólido/wireframe)
            _rsSolid = new RasterizerState { CullMode = CullMode.CullCounterClockwiseFace, FillMode = FillMode.Solid };
            _rsWire = new RasterizerState { CullMode = CullMode.None, FillMode = FillMode.WireFrame };

            _pos = Vector3.Zero;
            _vel = new Vector3(rnd.Next(-3, 3), 0, rnd.Next(-3, 3)); ;
            _accel = new Vector3(0, _g, 0);

            _H = 720;
            redim(_H);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            // Efecto básico con iluminación
            _effect = new BasicEffect(GraphicsDevice)
            {
                LightingEnabled = true,
                TextureEnabled = false,
                VertexColorEnabled = false,
                PreferPerPixelLighting = true,
                SpecularPower = 32f
            };
            _effect.EnableDefaultLighting();
            _effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-1f, -1f, -0.5f));
            _effect.DirectionalLight0.DiffuseColor = new Vector3(0.9f, 0.9f, 0.9f);

            // Construye la esfera dentro de Game1 (sin clases extra)
            BuildSphereMesh(_radius, _stacks, _slices);
        }

        /// <summary>
        /// Genera los buffers de la esfera siguiendo la parametrización:
        /// x = r*sinφ*cosθ,  y = r*cosφ,  z = r*sinφ*sinθ
        /// Recorremos φ en [0..π] (stacks) y θ en [0..2π] (slices).
        /// </summary>
        private void BuildSphereMesh(float radius, int stacks, int slices)
        {
            radius = Math.Max(0.0001f, radius);
            stacks = Math.Max(2, stacks);
            slices = Math.Max(3, slices);

            int vPerRing = slices + 1;                   // cerramos el anillo repitiendo el 1º vértice
            int vertexCount = (stacks + 1) * vPerRing;   // (stacks+1) anillos horizontales

            if (vertexCount > ushort.MaxValue)
                throw new InvalidOperationException("Demasiados vértices para índices de 16 bits.");

            var vertices = new VertexPositionNormalTexture[vertexCount];

            int v = 0;
            for (int i = 0; i <= stacks; i++)
            {
                float phi = MathF.PI * i / stacks;       // latitud   0..π
                float sinPhi = MathF.Sin(phi);
                float cosPhi = MathF.Cos(phi);

                for (int j = 0; j <= slices; j++)
                {
                    float theta = MathHelper.TwoPi * j / slices; // longitud 0..2π
                    float sinTheta = MathF.Sin(theta);
                    float cosTheta = MathF.Cos(theta);

                    // Fórmulas de la presentación:
                    float x = radius * sinPhi * cosTheta;
                    float y = radius * cosPhi;
                    float z = radius * sinPhi * sinTheta;

                    var pos = new Vector3(x, y, z);
                    var normal = Vector3.Normalize(pos);             // esfera centrada en el origen
                    var uv = new Vector2(theta / MathHelper.TwoPi,   // u: 0..1 → θ
                                          phi / MathF.PI);           // v: 0..1 → φ

                    vertices[v++] = new VertexPositionNormalTexture(pos, normal, uv);
                }
            }

            // Dos triángulos por cada "quad" (φ-θ)
            var indices = new List<ushort>(stacks * slices * 6);
            for (int i = 0; i < stacks; i++)
            {
                int ringStart = i * vPerRing;
                int nextRingStart = (i + 1) * vPerRing;

                for (int j = 0; j < slices; j++)
                {
                    ushort a = (ushort)(ringStart + j);
                    ushort b = (ushort)(nextRingStart + j);
                    ushort c = (ushort)(ringStart + j + 1);
                    ushort d = (ushort)(nextRingStart + j + 1);

                    // CCW mirando desde afuera
                    indices.Add(a); indices.Add(b); indices.Add(c);
                    indices.Add(c); indices.Add(b); indices.Add(d);
                }
            }

            // Subimos a GPU
            _vb?.Dispose();
            _ib?.Dispose();

            _vb = new VertexBuffer(GraphicsDevice, typeof(VertexPositionNormalTexture), vertices.Length, BufferUsage.WriteOnly);
            _vb.SetData(vertices);

            _ib = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, indices.Count, BufferUsage.WriteOnly);
            _ib.SetData(indices.ToArray());

            _primitiveCount = indices.Count / 3;
        }

        protected override void Update(GameTime gameTime)
        {
            var k = Keyboard.GetState();
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (k.IsKeyDown(WINDOW_PLUS))
            {
                _H++;
                redim(_H);
            }
            else if (k.IsKeyDown(WINDOW_MINUS))
            {
                _H--;
                redim(_H);
            }

            if (k.IsKeyDown(LEFT)) _accel.X = -3;
            else if (k.IsKeyDown(RIGHT)) _accel.X = 3;
            else _accel.X = 0;

            if (k.IsKeyDown(FRONT)) _accel.Z = -3;
            else if (k.IsKeyDown(BACK)) _accel.Z = 3;
            else _accel.Z = 0;

            _vel += _accel * deltaTime;
            _vel *= 0.995f;
            _pos += _vel * deltaTime;

            if (_pos.Y + _radius < -worldLimits)
            {
                _pos.Y = -worldLimits - _radius;
                _vel.Y *= -1 * 0.9f;
            }

            if (_pos.X + _radius >= worldLimits * 1.5f) _vel.X *= -1;
            if (_pos.X + _radius <= -worldLimits * 1.5f) _vel.X *= -1;
            if (_pos.Z + _radius >= worldLimits) _vel.Z *= -1;
            if (_pos.Z + _radius <= -worldLimits) _vel.Z *= -1;

            if (_pos.Y == -worldLimits - _radius)
                if (k.IsKeyDown(JUMP))
                    _vel.Y = 7.5f;

            if (k.IsKeyDown(EXIT))
                Exit();

            // Wireframe mientras esté presionada W
            _wireframe = k.IsKeyDown(Keys.I);


            Console.WriteLine($"X: {MathF.Round(_pos.X, 2)}, " +
                $"Y: {MathF.Round(_pos.Y, 2)}, " +
                $"Z: {MathF.Round(_pos.Z, 2)}, " +
                $"Vel X: {MathF.Round(_vel.X, 2)}, " +
                $"Vel Y: {MathF.Round(_vel.Y, 2)}, " +
                $"Vel Z: {MathF.Round(_vel.Z, 2)}, " +
                $"Accel X: {MathF.Round(_accel.X, 2)}, " +
                $"Accel Y: {MathF.Round(_accel.Y, 2)}, " +
                $"Accel Z: {MathF.Round(_accel.Z, 2)}"
            );

            if (_rotate)
                _angle += (float)gameTime.ElapsedGameTime.TotalSeconds * 0.7f;

            // Rotamos en Y (meridianos θ) y un poco en X para percibir normales

            _world =
                //Matrix.CreateRotationY(_angle) *
                Matrix.CreateTranslation(_pos) *
                Matrix.CreateRotationX(0.25f);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(18, 24, 32));
            GraphicsDevice.RasterizerState = _wireframe ? _rsWire : _rsSolid;

            GraphicsDevice.SetVertexBuffer(_vb);
            GraphicsDevice.Indices = _ib;

            _effect.World = _world;
            _effect.View = _view;
            _effect.Projection = _proj;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawIndexedPrimitives(
                    primitiveType: PrimitiveType.TriangleList,
                    baseVertex: 0,
                    minVertexIndex: 0,
                    numVertices: _vb.VertexCount,
                    startIndex: 0,
                    primitiveCount: _primitiveCount
                );
            }

            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            _vb?.Dispose();
            _ib?.Dispose();
            _effect?.Dispose();
            base.UnloadContent();
        }

        private void redim(int H)
        {
            int W = (H * 16) / 9;
            _gdm.PreferredBackBufferHeight = H;
            _gdm.PreferredBackBufferWidth = W;
            _gdm.ApplyChanges();
        }

    }
}
