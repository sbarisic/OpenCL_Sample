using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenCL;

namespace Toy1
{
    unsafe class CLKernel
    {
        public nint kernel;
        CLContext Ctx;

        public CLKernel(CLContext Ctx, CLProgram Prog, string func_name)
        {
            this.Ctx = Ctx;
            kernel = Ctx.cL.CreateKernel(Prog.program, func_name, null);

            if (kernel == IntPtr.Zero)
            {
                throw new Exception("Failed to create kernel");
            }
        }

        public void SetKernelArg(int ArgNum, CLObject Obj)
        {
            Ctx.cL.SetKernelArg(kernel, (uint)ArgNum, (nuint)sizeof(nint), ref Obj.obj);
        }
    }

    unsafe class CLProgram
    {
        public nint program;

        public CLProgram(CLContext Ctx, CLCommandQueue CQueue, string fileName)
        {
            if (!File.Exists(fileName))
                throw new Exception($"File does not exist: {fileName}");


            using StreamReader sr = new StreamReader(fileName);
            string clStr = sr.ReadToEnd();

            program = Ctx.cL.CreateProgramWithSource(Ctx.context, 1, new string[] { clStr }, null, null);
            if (program == IntPtr.Zero)
                throw new Exception("Failed to create CL program from source.");

            var errNum = Ctx.cL.BuildProgram(program, 0, null, (byte*)null, null, null);

            if (errNum != (int)ErrorCodes.Success)
            {
                _ = Ctx.cL.GetProgramBuildInfo(program, CQueue.device, ProgramBuildInfo.BuildLog, 0, null, out nuint buildLogSize);
                byte[] log = new byte[buildLogSize / (nuint)sizeof(byte)];

                fixed (void* pValue = log)
                {
                    Ctx.cL.GetProgramBuildInfo(program, CQueue.device, ProgramBuildInfo.BuildLog, buildLogSize, pValue, null);
                }

                string? build_log = Encoding.UTF8.GetString(log);

                //Console.WriteLine("Error in kernel: ");
                Console.WriteLine("=============== OpenCL Program Build Info ================");
                Console.WriteLine(build_log);
                Console.WriteLine("==========================================================");

                Ctx.cL.ReleaseProgram(program);
                throw new Exception("Exception!");
            }
        }
    }

    unsafe class CLCommandQueue
    {
        public nint device;
        public nint commandQueue;

        public CLCommandQueue(CLContext Ctx)
        {
            int errNum = Ctx.cL.GetContextInfo(Ctx.context, ContextInfo.Devices, 0, null, out nuint deviceBufferSize);
            if (errNum != (int)ErrorCodes.Success)
                throw new Exception("Failed call to clGetContextInfo(...,GL_CONTEXT_DEVICES,...)");

            if (deviceBufferSize <= 0)
                throw new Exception("No devices available.");


            nint[] devices = new nint[deviceBufferSize / (nuint)sizeof(nuint)];
            fixed (void* pValue = devices)
            {
                int er = Ctx.cL.GetContextInfo(Ctx.context, ContextInfo.Devices, deviceBufferSize, pValue, null);

            }

            if (errNum != (int)ErrorCodes.Success)
            {
                throw new Exception("Failed to get device IDs");
            }

            // In this example, we just choose the first available device.  In a
            // real program, you would likely use all available devices or choose
            // the highest performance device based on OpenCL device queries
            commandQueue = Ctx.cL.CreateCommandQueue(Ctx.context, devices[0], CommandQueueProperties.None, null);

            if (commandQueue == IntPtr.Zero)
                throw new Exception("Failed to create commandQueue for device 0");

            device = devices[0];
        }
    }

    unsafe class CLObject
    {
        public nint obj;

        public CLObject(CLContext Ctx, MemFlags Flags, float[] Arr)
        {
            fixed (void* ArrPtr = Arr)
            {
                obj = Ctx.cL.CreateBuffer(Ctx.context, Flags, (nuint)(sizeof(float) * Arr.Length), ArrPtr, null);
                ErrCheck();
            }
        }

        public CLObject(CLContext Ctx, MemFlags Flags, int Len)
        {
            obj = Ctx.cL.CreateBuffer(Ctx.context, Flags, (nuint)(sizeof(float) * Len), null, null);
            ErrCheck();
        }

        void ErrCheck()
        {
            if (obj == IntPtr.Zero)
                throw new Exception("Error creating memory objects.");
        }
    }

    unsafe class CLContext
    {
        public CL cL;
        public nint context;

        public CLContext()
        {
            cL = CL.GetApi();

            var errNum = cL.GetPlatformIDs(1, out nint firstPlatformId, out uint numPlatforms);

            if (errNum != (int)ErrorCodes.Success || numPlatforms <= 0)
                throw new Exception("Failed to find any OpenCL platforms.");

            // Next, create an OpenCL context on the platform.  Attempt to
            // create a GPU-based context, and if that fails, try to create
            // a CPU-based context.
            nint[] contextProperties = new nint[]
            {
                (nint)ContextProperties.Platform,
                firstPlatformId,
                0
            };

            fixed (nint* p = contextProperties)
            {
                context = cL.CreateContextFromType(p, DeviceType.Gpu, null, null, out errNum);
                if (errNum != (int)ErrorCodes.Success)
                {
                    Console.WriteLine("Could not create GPU context, trying CPU...");
                    context = cL.CreateContextFromType(p, DeviceType.Cpu, null, null, out errNum);

                    if (errNum != (int)ErrorCodes.Success)
                    {
                        throw new Exception("Failed to create an OpenCL GPU or CPU context.");
                    }
                }
            }
        }

        public void Exec(CLCommandQueue CLQueue, CLKernel Krn, int WorkDim, nuint[] GlobalWorkSize, nuint[] LocalWorkSize)
        {
            cL.EnqueueNdrangeKernel(CLQueue.commandQueue, Krn.kernel, (uint)WorkDim, (nuint*)null, GlobalWorkSize, LocalWorkSize, 0, (nint*)null, (nint*)null);
        }

        public CLObject AllocateObject_Input(float[] Arr)
        {
            return new CLObject(this, MemFlags.ReadOnly | MemFlags.CopyHostPtr, Arr);
        }

        public CLObject AllocateObject_Output(int Len)
        {
            return new CLObject(this, MemFlags.ReadWrite, Len);
        }
    }
}
