using Silk.NET.OpenCL;

namespace Toy1
{
    internal class Program
    {
        const int ARRAY_SIZE = 1000;

        static unsafe void Main(string[] args)
        {
            // Create an OpenCL context on first available platform
            CLContext Ctx = new CLContext();


            // Create a command-queue on the first device available
            // on the created context
            CLCommandQueue CQueue = new CLCommandQueue(Ctx);


            // Create OpenCL program from HelloWorld.cl kernel source
            CLProgram Prog = new CLProgram(Ctx, CQueue, "data/kernel.cl");

            // Create OpenCL kernel
            CLKernel Krn = new CLKernel(Ctx, Prog, "hello_kernel");

            // Create memory objects that will be used as arguments to
            // kernel.  First create host memory arrays that will be
            // used to store the arguments to the kernel
            float[] result = new float[ARRAY_SIZE];
            float[] a = new float[ARRAY_SIZE];
            float[] b = new float[ARRAY_SIZE];
            for (int i = 0; i < ARRAY_SIZE; i++)
            {
                a[i] = (float)i;
                b[i] = (float)(i * 2);
            }

            nint[] memObjects = new nint[3];
            if (!CreateMemObjects(cl, context, memObjects, a, b))
            {
                Cleanup(cl, context, commandQueue, program, kernel, memObjects);
                return;
            }

            // Set the kernel arguments (result, a, b)
            int errNum = cl.SetKernelArg(kernel, 0, (nuint)sizeof(nint), memObjects[0]);
            errNum |= cl.SetKernelArg(kernel, 1, (nuint)sizeof(nint), memObjects[1]);
            errNum |= cl.SetKernelArg(kernel, 2, (nuint)sizeof(nint), memObjects[2]);

            if (errNum != (int)ErrorCodes.Success)
            {
                Console.WriteLine("Error setting kernel arguments.");
                Cleanup(cl, context, commandQueue, program, kernel, memObjects);
                return;
            }

            nuint[] globalWorkSize = new nuint[1] { ARRAY_SIZE };
            nuint[] localWorkSize = new nuint[1] { 1 };

            // Queue the kernel up for execution across the array
            errNum = cl.EnqueueNdrangeKernel(commandQueue, kernel, 1, (nuint*)null, globalWorkSize, localWorkSize, 0, (nint*)null, (nint*)null);
            if (errNum != (int)ErrorCodes.Success)
            {
                Console.WriteLine("Error queuing kernel for execution.");
                Cleanup(cl, context, commandQueue, program, kernel, memObjects);
                return;
            }

            fixed (void* pValue = result)
            {
                // Read the output buffer back to the Host
                errNum = cl.EnqueueReadBuffer(commandQueue, memObjects[2], true, 0, ARRAY_SIZE * sizeof(float), pValue, 0, null, null);
                if (errNum != (int)ErrorCodes.Success)
                {
                    Console.WriteLine("Error reading result buffer.");
                    Cleanup(cl, context, commandQueue, program, kernel, memObjects);
                    return;
                }
            }

            // Output the result buffer
            for (int i = 0; i < ARRAY_SIZE; i++)
            {
                Console.WriteLine(result[i]);
            }

            Console.WriteLine("Executed program succesfully.");
            Cleanup(cl, context, commandQueue, program, kernel, memObjects);

            Console.WriteLine("Done!");
            Console.ReadLine();
        }
    }
}
