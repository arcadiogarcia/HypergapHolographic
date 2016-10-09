using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using HypergapHolographic.Common;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;


namespace HypergapHolographic.Content
{
    class Sprite : Disposer
    {
        private Vector3 position;
        private String spriteImg;


        private  SharpDX.Direct3D11.Buffer modelConstantBuffer;
        // System resources for cube geometry.
        private ModelConstantBuffer modelConstantBufferData;
        private int indexCount = 0;
        private SharpDX.Direct3D11.Buffer indexBuffer;
        private SharpDX.Direct3D11.Buffer vertexBuffer;


        public Sprite(float x, float y, float z, String spriteImg)
        {
            position = new Vector3(x,y,z);
            this.spriteImg = spriteImg;
        }

        public void Update(StepTimer timer, DeviceResources deviceResources)
        {
            // Rotate the cube.
            // Convert degrees to radians, then convert seconds to rotation angle.
            float radiansPerSecond = 45.0f * ((float)Math.PI / 180.0f);
            double totalRotation = timer.TotalSeconds * radiansPerSecond;
            float radians = (float)System.Math.IEEERemainder(totalRotation, 2 * Math.PI);
            Matrix4x4 modelRotation = Matrix4x4.CreateFromAxisAngle(new Vector3(0, 1, 0), -radians);


            // Position the cube.
            Matrix4x4 modelTranslation = Matrix4x4.CreateTranslation(position);


            // Multiply to get the transform matrix.
            // Note that this transform does not enforce a particular coordinate system. The calling
            // class is responsible for rendering this content in a consistent manner.
            Matrix4x4 modelTransform = modelRotation * modelTranslation;

            // The view and projection matrices are provided by the system; they are associated
            // with holographic cameras, and updated on a per-camera basis.
            // Here, we provide the model transform for the sample hologram. The model transform
            // matrix is transposed to prepare it for the shader.
            this.modelConstantBufferData.model = Matrix4x4.Transpose(modelTransform);


            // Use the D3D device context to update Direct3D device-based resources.
            var context = deviceResources.D3DDeviceContext;

            // Update the model transform buffer for the hologram.
            context.UpdateSubresource(ref this.modelConstantBufferData, this.modelConstantBuffer);
        }

        public void CreateDeviceDependentResourcesAsync(DeviceResources deviceResources)
        {
            float scaleFactor = 0.00025f;
            var image = TextureLoader.LoadBitmap(new SharpDX.WIC.ImagingFactory2(), spriteImg);
            var height = image.Size.Height * scaleFactor;
            var width = image.Size.Width * scaleFactor;

            BlendStateDescription blendSdesc = new BlendStateDescription();
            blendSdesc.IndependentBlendEnable = false;
            blendSdesc.AlphaToCoverageEnable = false;
            blendSdesc.RenderTarget[0].IsBlendEnabled = true;
            blendSdesc.RenderTarget[0].SourceBlend = BlendOption.SourceAlpha;
            blendSdesc.RenderTarget[0].DestinationBlend = BlendOption.InverseSourceAlpha;
            blendSdesc.RenderTarget[0].BlendOperation = BlendOperation.Maximum;
            blendSdesc.RenderTarget[0].SourceAlphaBlend = BlendOption.One;
            blendSdesc.RenderTarget[0].DestinationAlphaBlend = BlendOption.One;
            blendSdesc.RenderTarget[0].AlphaBlendOperation = BlendOperation.Add;
            blendSdesc.RenderTarget[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;

            BlendState blendS = new BlendState(deviceResources.D3DDevice, blendSdesc);
            deviceResources.D3DDeviceContext.OutputMerger.SetBlendState(blendS);

            // Load mesh vertices. Each vertex has a position and a color.
            // Note that the cube size has changed from the default DirectX app
            // template. Windows Holographic is scaled in meters, so to draw the
            // cube at a comfortable size we made the cube width 0.2 m (20 cm).
            VertexPositionTexture[] cubeVertices =
            {
                new VertexPositionTexture(new Vector3(0f, -1f*height, -1f*width), new Vector2(0.0f, 1.0f)),
                new VertexPositionTexture(new Vector3(0f, -1f*height,  1f*width), new Vector2(1.0f, 1.0f)),
                new VertexPositionTexture(new Vector3(0f,  1f*height, -1f*width), new Vector2(0.0f, 0.0f)),
                new VertexPositionTexture(new Vector3(0f,  1f*height,  1f*width), new Vector2(1.0f, 0.0f))
            };

            vertexBuffer = this.ToDispose(SharpDX.Direct3D11.Buffer.Create(
                deviceResources.D3DDevice,
                SharpDX.Direct3D11.BindFlags.VertexBuffer,
                cubeVertices));

            // Load mesh indices. Each trio of indices represents
            // a triangle to be rendered on the screen.
            // For example: 0,2,1 means that the vertices with indexes
            // 0, 2 and 1 from the vertex buffer compose the 
            // first triangle of this mesh.
            ushort[] cubeIndices =
            {
                2,1,0, // -x
                2,3,1,
                //back face
                2,0,1, // -x
                2,1,3,
            };

            indexCount = cubeIndices.Length;
            indexBuffer = this.ToDispose(SharpDX.Direct3D11.Buffer.Create(
                deviceResources.D3DDevice,
                SharpDX.Direct3D11.BindFlags.IndexBuffer,
                cubeIndices));
            // Create a constant buffer to store the model matrix.
            modelConstantBuffer = this.ToDispose(SharpDX.Direct3D11.Buffer.Create(
                deviceResources.D3DDevice,
                SharpDX.Direct3D11.BindFlags.ConstantBuffer,
                ref modelConstantBufferData));

            //Load the image
            var texture = TextureLoader.CreateTexture2DFromBitmap(deviceResources.D3DDevice, image);
            ShaderResourceView textureView = new ShaderResourceView(deviceResources.D3DDevice, texture);
            deviceResources.D3DDeviceContext.PixelShader.SetShaderResource(0, textureView);
            //Load the sampler
            SamplerStateDescription samplerDesc = new SamplerStateDescription();
            samplerDesc.AddressU = TextureAddressMode.Wrap;
            samplerDesc.AddressV = TextureAddressMode.Wrap;
            samplerDesc.AddressW = TextureAddressMode.Wrap;
            samplerDesc.ComparisonFunction = Comparison.Never;
            samplerDesc.Filter = Filter.MinMagMipLinear;
            samplerDesc.MaximumLod = float.MaxValue;
            SamplerState sampler = new SamplerState(deviceResources.D3DDevice, samplerDesc);
            deviceResources.D3DDeviceContext.PixelShader.SetSampler(0, sampler);
        }

        internal void Render(DeviceContext3 context, InputLayout inputLayout, VertexShader vertexShader, bool usingVprtShaders, GeometryShader geometryShader, PixelShader pixelShader)
        {
            // Each vertex is one instance of the VertexPositionColor struct.
            int stride = SharpDX.Utilities.SizeOf<VertexPositionTexture>();
            int offset = 0;
            var bufferBinding = new SharpDX.Direct3D11.VertexBufferBinding(this.vertexBuffer, stride, offset);
            context.InputAssembler.SetVertexBuffers(0, bufferBinding);
            context.InputAssembler.SetIndexBuffer(
                this.indexBuffer,
                SharpDX.DXGI.Format.R16_UInt, // Each index is one 16-bit unsigned integer (short).
                0);
            context.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            context.InputAssembler.InputLayout = inputLayout;

            // Attach the vertex shader.
            context.VertexShader.SetShader(vertexShader, null, 0);
            // Apply the model constant buffer to the vertex shader.
            context.VertexShader.SetConstantBuffers(0, this.modelConstantBuffer);

            if (!usingVprtShaders)
            {
                // On devices that do not support the D3D11_FEATURE_D3D11_OPTIONS3::
                // VPAndRTArrayIndexFromAnyShaderFeedingRasterizer optional feature,
                // a pass-through geometry shader is used to set the render target 
                // array index.
                context.GeometryShader.SetShader(geometryShader, null, 0);
            }

            // Attach the pixel shader.
            context.PixelShader.SetShader(pixelShader, null, 0);

            // Draw the objects.
            context.DrawIndexedInstanced(
                indexCount,     // Index count per instance.
                2,              // Instance count.
                0,              // Start index location.
                0,              // Base vertex location.
                0               // Start instance location.
                );
        }

        /// <summary>
        /// Releases device-based resources.
        /// </summary>
        public void ReleaseDeviceDependentResources()
        {
            this.RemoveAndDispose(ref modelConstantBuffer);
            this.RemoveAndDispose(ref vertexBuffer);
            this.RemoveAndDispose(ref indexBuffer);
        }
    }
}
