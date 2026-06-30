//****************************************************************************
// Also note that we've supplied a helpful debugging function called checkCudaErrors.
// You should wrap your allocation and copying statements like we've done in the
// code we're supplying you. Here is an example of the unsafe way to allocate
// memory on the GPU:
//
// cudaMalloc(&d_red, sizeof(unsigned char) * numRows * numCols);
//
// Here is an example of the safe way to do the same thing:
//
// checkCudaErrors(cudaMalloc(&d_red, sizeof(unsigned char) * numRows * numCols));
//****************************************************************************

#include <iostream>
#include <iomanip>
#include <cuda.h>
#include <cuda_runtime.h>
#include <cuda_runtime_api.h>

#define checkCudaErrors(val) check( (val), #val, __FILE__, __LINE__)

template<typename T>
void check(T err, const char* const func, const char* const file, const int line) {
  if (err != cudaSuccess) {
    std::cerr << "CUDA error at: " << file << ":" << line << std::endl;
    std::cerr << cudaGetErrorString(err) << " " << func << std::endl;
    exit(1);
  }
}

const int TILE_WIDTH=32;
const int TILE_HEIGHT=32;
// Variable global que debe ser tratadas como si fueran constantes si no es _CANNY_EDGE
const int FILTERSIZE = 5;

// Defines a utilizar en caso de realizar esa funcionalidad (con ifdef y ifndef)
#define _CONSTANT_MEMORY 
#define _SHARED_MEMORY
#define _CANNY_EDGE

// Global variables for Blur and Canny Edge
unsigned char* d_red, * d_green, * d_blue,
               * d_grey, * d_grey_filtered_char, 
               * d_outputImageGrey, * d_threshold_out;

float* d_grey_filtered, * d_Gx, * d_Gy, * d_mag, * d_suppression_out,
     * d_red_f, * d_green_f, * d_blue_f;

// Max pixel value for treshold
int* maxPixel_int;
float maxPixel;

// Constant memory allocation
//array for filters
__constant__ float constant_filter[FILTERSIZE * FILTERSIZE]; // filter permanenlty allocated

__global__ // Function call by CPU and executed by GPU
void convolution(unsigned char* const input,
                 float* const output,
                 int numRows, int numCols,
                 const int filterWidth,
                 int const id_filter
                 #ifndef _CONSTANT_MEMORY
                    , const float* const d_filter
                 #endif
                 ) {

#ifdef _CONSTANT_MEMORY
    #define used_filter constant_filter
#else
    #define used_filter d_filter
#endif

// Coordinates --> gloab IDs 
int x = blockIdx.x * blockDim.x + threadIdx.x;
int y = blockIdx.y * blockDim.y + threadIdx.y;
const int radius = filterWidth / 2; // 2 in Blur, 1 in Sobel

// SHARED MEMORY
#ifdef _SHARED_MEMORY
    extern __shared__ float shared_memory[];

    // I'm saving extra pixel in the shared memory --> extra boundaries needed 
    int first_x = blockIdx.x * blockDim.x - radius;  
    int first_y = blockIdx.y * blockDim.y - radius;

    int dim_SM = TILE_WIDTH + 2 * radius;
    
    // cicle for each block --> it goes from zero to shared memory array dimension
    for (int i = threadIdx.y * blockDim.x + threadIdx.x;  i < dim_SM * dim_SM; i += blockDim.x * blockDim.y) {
        //i = threadIdx.y * blockDim.x + threadIdx.x --> MATRIX INTO ARRAY INDEX
        // local indexes in the block
        // for is in case dim SharedMemory used > block capacity 
        // --> every StreamMultipocessor has its on SM
         int r = i / dim_SM;
         int c = i % dim_SM; // tracing at which whole block the SM is copying on the SHARED MEMORY
         // the unit of SM encharghed to do it --> LSU (Load/Store Unit).

         // Global coordintaes of the current pixel (surrounding (y,x) pixel))
         int global_r = first_y + r;
         int global_c = first_x + c;

         if (global_r < 0) global_r = 0;
         else if (global_r >= numRows) global_r = numRows - 1;

         if (global_c < 0) global_c = 0;
         else if (global_c >= numCols) global_c = numCols - 1;

         shared_memory[r * dim_SM + c] = (float)input[global_r * numCols + global_c];
    }

    __syncthreads();

#endif

    // Check boundaries
    if (x >= numCols || y >= numRows)
    {
        return;
    }

    float finalPixel = 0.0f;

    int shared_r = threadIdx.y + radius;
    int shared_c = threadIdx.x + radius;

    for (int r = -radius; r <= radius; ++r) {
        for (int c = -radius; c <= radius; ++c) {

            // center = radius --> adding from - r = -radius to r = radius
            float filterWeight = used_filter[(r + radius) * filterWidth + (c + radius)]; // + radius because filter indexes strart from 0
            float pixelValue;

            #ifdef _SHARED_MEMORY
                    pixelValue = shared_memory[(shared_r + r) * dim_SM + (shared_c + c)];

            #else // Global Memory
                    int Y = y + r;
                    int X = x + c;

                    if (Y < 0) Y = 0;
                    else if (Y >= numRows) Y = numRows - 1;

                    if (X < 0) X = 0;
                    else if (X >= numCols) X = numCols - 1;

                    pixelValue = (float)(input[Y * numCols + X]);
            #endif

            finalPixel += pixelValue * filterWeight;
        }
    }
    if (id_filter == 0 || id_filter == 1) {// IN case its blur or laplacian filter
        output[y * numCols + x] = fminf(255.0f, fmaxf(0.0f, finalPixel));
    }
    else {
        output[y * numCols + x] = finalPixel;
    }

#undef used_filter
}

/*
// Convolution version with float for Gx e Gy of Sobel filter
__global__ 
void convolution_float(const unsigned char* input, 
                        float* output, 
                        int numRows, int numCols,
                        int filterWidth
                        #ifndef _CONSTANT_MEMORY
                        , const float* const d_filter
                        #endif
                        ) {

    #ifdef _CONSTANT_MEMORY
    #define used_filter constant_filter
    #else
    #define used_filter d_filter
    #endif

    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;
    
    if (x >= numCols || y >= numRows) return;

    float result = 0.0f;
    int radius = filterWidth / 2;

    for (int r = -radius; r <= radius; ++r) {
        for (int c = -radius; c <= radius; ++c) {
            
            /*int curY = y + r;
            int curX = x + c;

            if (curY < 0) curY = 0;
            else if (curY >= numRows) curY = numRows - 1;

            if (curX < 0) curX = 0;
            else if (curX >= numCols) curX = numCols - 1;

            float filterWeight = used_filter[(r + radius) * filterWidth + (c + radius)];
            result += filterWeight * static_cast<float>(input[curY * numCols + curX]) ;
            

            int curY = y + r;
            int curX = x + c;
            float pixelValue = 0.0f; 

            if (curY >= 0 && curY < numRows && curX >= 0 && curX < numCols) {
                pixelValue = static_cast<float>(input[curY * numCols + curX]);
            }

            float filterWeight = used_filter[(r + radius) * filterWidth + (c + radius)];
            result += filterWeight * pixelValue;
        }
    }
    output[y * numCols + x] = result; 

#undef used_filter
}*/

// Fucntion to convert image to grey scale (instead of using opencv)
__global__
void colorToGrey(const uchar4* const rgbaImage, 
                 unsigned char* const greyImage, 
                 int numRows, int numCols)
{
    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;

    if (x < numCols && y < numRows) {
        int pos = y * numCols + x;

        uchar4 pixel = rgbaImage[pos];

        // Weightened to find correspondent grey value
        greyImage[pos] = (unsigned char)(0.299f * pixel.x + 0.587f * pixel.y + 0.114f * pixel.z);
    }
}

//This kernel takes in an image represented as a uchar4 and splits
//it into three images consisting of only one color channel each
__global__
void separateChannels(const uchar4* const inputImageRGBA, 
                      int numRows, int numCols, unsigned char* const redChannel,
                      unsigned char* const greenChannel, 
                      unsigned char* const blueChannel)
    {

    // Our bloque is a grid of 32x32 threads 
    // Detecting threads (x,y) in the grid
    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;

    if (x >= numCols || y >= numRows) {
        return;
    }

    // Computing position in the memory array and extracting its color
    int pos = y * numCols + x;

    uchar4 rgba = inputImageRGBA[pos];

    // Distributing in each channel
    redChannel[pos] = rgba.x;
    greenChannel[pos] = rgba.y;
    blueChannel[pos] = rgba.z;
}

//This kernel takes in three color channels and recombines them
//into one image. The alpha channel is set to 255 to represent
//that this image has no transparency.
__global__
void recombineChannels(const unsigned char* const redChannel,
                       const unsigned char* const greenChannel,
                       const unsigned char* const blueChannel,
                       uchar4* const outputImageRGBA,
                       int numRows,
                       int numCols)
{
  const int2 thread_2D_pos = make_int2( blockIdx.x * blockDim.x + threadIdx.x,
                                        blockIdx.y * blockDim.y + threadIdx.y);

  const int thread_1D_pos = thread_2D_pos.y * numCols + thread_2D_pos.x;

  //make sure we don't try and access memory outside the image
  //by having any threads mapped there return early
  if (thread_2D_pos.x >= numCols || thread_2D_pos.y >= numRows)
    return;

  unsigned char red   = redChannel[thread_1D_pos];
  unsigned char green = greenChannel[thread_1D_pos];
  unsigned char blue  = blueChannel[thread_1D_pos];

  //Alpha should be 255 for no transparency
  uchar4 outputPixel = make_uchar4(red, green, blue, 255);

  outputImageRGBA[thread_1D_pos] = outputPixel;
}

// Kernel to compute magnitude given Sobel gradients
__global__ 
void magnitude_kernel(const float* Gx, const float* Gy, float* mag, 
                      int numRows, int numCols) {

    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;

    if (x < numCols && y < numRows) {
        int pos = y * numCols + x;
        // Native CUDA function
        mag[pos] = hypotf(Gx[pos], Gy[pos]);
    }

}

// Kenrel to compute find pixel value
__global__ void findMax(float* input, int* maxVal, int size) {

    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;
    int i = y * gridDim.x * blockDim.x + x; 

    if (i < size) {
        float val = input[i];
        atomicMax(maxVal, (int)(val));
    }
}

// Kernel to suppress non_maximum pixel
__global__ 
void suppression_kernel(const float* mag, 
                        const float* Gx, const float* Gy, 
                        float* img_out, 
                        int numRows, int numCols) {

    int x = blockIdx.x * blockDim.x + threadIdx.x; 
    int y = blockIdx.y * blockDim.y + threadIdx.y; 

    if (y > 0 && y < numRows - 1 && x > 0 && x < numCols - 1)
    {
        int pos = y * numCols + x;

        // Computing angle -->  atan2f gives back angels betwenn [-pi;pi];
        float angle = atan2f(Gy[pos], Gx[pos]) * 180.0f / 3.14159265f;
        if (angle < 0) angle += 180.0f; // Adding 180 so less checks on out values are needed

        // Security initial value
        float q = 255.0f; 
        float r = 255.0f;

        // Direction based on arctg --> check the direction of the total gradient
        if ((0 <= angle && angle < 22.5) || (157.5 <= angle && angle <= 180)) {
            q = mag[pos + 1];   
            r = mag[pos - 1];    
        }
        else if (22.5 <= angle && angle < 67.5) {
            q = mag[pos + numCols - 1];
            r = mag[pos - numCols + 1];
        }
        else if (67.5 <= angle && angle < 112.5) {
            q = mag[pos + numCols];
            r = mag[pos - numCols];
        }
        else if (112.5 <= angle && angle < 157.5) {
            q = mag[pos - numCols - 1];
            r = mag[pos + numCols + 1];
        }

        // Suppression of pixel
        if (mag[pos] >= q && mag[pos] >= r) {
            img_out[pos] = mag[pos];
        }
        else {
            img_out[pos] = 0;
        }
    }
}

// Kernel to determine pixel strengh
// It must be separated from the NM_suppression kernel to avoid race coniditon
__global__ 
void threshold_kernel(const float* nms_in, 
                      unsigned char* img_out, 
                      int numRows, int numCols, 
                      float lowThreshold, 
                      float highThreshold) {
   
    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;

    if (x < numCols && y < numRows) {
        int pos = y * numCols + x;
        float pixel = nms_in[pos];

        if (pixel >= highThreshold) {
            img_out[pos] = 255; // STRONG
        }
        else if (pixel >= lowThreshold) {
            img_out[pos] = 50;  // WEAK
        }
        else {
            img_out[pos] = 0;   // ZERO
        }
    }

}

// Kernel for Hysteresis porcess od Canny Edge
__global__ 
void hysteresis_kernel(const unsigned char* img_in, 
                       unsigned char* img_out, 
                       int numRows, int numCols) {
   
    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;

    if (y > 0 && y < numRows - 1 && x > 0 && x < numCols - 1) 
    {
        int pos = y * numCols + x;
        unsigned char pixel = img_in[pos];

        if (pixel == 50) {

            bool connected = false;
            for (int ny = -1; ny <= 1; ny++) {
                for (int nx = -1; nx <= 1; nx++) {
                    if (img_in[(y + ny) * numCols + (x + nx)] == 255)
                    {
                        connected = true;
                        break;
                    }
                }
                if (connected) break; // to exit from second loop
            }

            if (connected) img_out[pos] = 255;

            else img_out[pos] = 0;
        }

        else
        {
            img_out[pos] = pixel;
        }
    }
}

// Kenrnel to convert one float array (G Sobel) to unchar array (grey or one color image)
__global__
void floatToChar_kernel(const float* input, 
                        unsigned char* output, 
                        int numRows, int numCols) {

    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;

    if (x < numCols && y < numRows) {
        int pos = y * numCols + x;
        // Clamping: se il valore supera 255 lo tagliamo, altrimenti diventa spazzatura
        float val = input[pos];
        output[pos] = (unsigned char)fmaxf(0.0f, fminf(255.0f, val));
    }
}

void allocateMemoryGPU(const size_t numRowsImage, const size_t numColsImage)
{

  //allocate memory for the three different channels
  checkCudaErrors(cudaMalloc(&d_red,   sizeof(unsigned char) * numRowsImage * numColsImage));
  checkCudaErrors(cudaMalloc(&d_green, sizeof(unsigned char) * numRowsImage * numColsImage));
  checkCudaErrors(cudaMalloc(&d_blue,  sizeof(unsigned char) * numRowsImage * numColsImage));
}

void allocateFilterAndCopyToGPU(const float *h_filter, const size_t filterWidth, float **d_filter)
{
    size_t filterSize = sizeof(float) * filterWidth * filterWidth;

    checkCudaErrors(cudaMalloc(d_filter, filterSize));

    // *d_filter --> desreferenciación SP, dereferencing ENG
    checkCudaErrors(cudaMemcpy(*d_filter, h_filter, filterSize, cudaMemcpyHostToDevice));
}

//Free all the memory that we allocated
//TODO: make sure you free any arrays that you allocated
void cleanupGPU() {
    checkCudaErrors(cudaFree(d_red));
    checkCudaErrors(cudaFree(d_green));
    checkCudaErrors(cudaFree(d_blue));
    checkCudaErrors(cudaFree(d_grey));
    checkCudaErrors(cudaFree(d_grey_filtered));
    checkCudaErrors(cudaFree(d_Gx)); 
    checkCudaErrors(cudaFree(d_Gy)); 
    checkCudaErrors(cudaFree(d_mag));
    checkCudaErrors(cudaFree(d_suppression_out));
    checkCudaErrors(cudaFree(d_threshold_out));
    // Clean of filter allocation done in the box_filter function
}


void create_filter(float **h_filter, int *filterWidth, int id_filter){

  const int KernelWidth = FILTERSIZE; //OJO CON EL TAMAÑO DEL FILTRO//
  *filterWidth = KernelWidth;

  //create and fill the filter we will convolve with
  *h_filter = new float[KernelWidth * KernelWidth];
  
  switch ( id_filter ) 
  {

    case 0: //Filtro gaussiano: blur
    {
      const float KernelSigma = 2.;

      float filterSum = 0.f; // for normalization

      for (int r = -KernelWidth/2; r <= KernelWidth/2; ++r) {
        for (int c = -KernelWidth/2; c <= KernelWidth/2; ++c) {
          float filterValue = expf( -(float)(c * c + r * r) / (2.f * KernelSigma * KernelSigma));
          (*h_filter)[(r + KernelWidth/2) * KernelWidth + c + KernelWidth/2] = filterValue;
          filterSum += filterValue;
        }
      }

      float normalizationFactor = 1.f / filterSum;

      for (int r = -KernelWidth/2; r <= KernelWidth/2; ++r) {
        for (int c = -KernelWidth/2; c <= KernelWidth/2; ++c) {
          (*h_filter)[(r + KernelWidth/2) * KernelWidth + c + KernelWidth/2] *= normalizationFactor;
        }
      }
    }
    break;

    case 1: // Filtro Laplaciano 5x5 
    { 
      (*h_filter)[0] = 0;   (*h_filter)[1] = 0;    (*h_filter)[2] = -1.;  (*h_filter)[3] = 0;    (*h_filter)[4] = 0;
      (*h_filter)[5] = 0;  (*h_filter)[6] = -1.;  (*h_filter)[7] = -2.;  (*h_filter)[8] = -1.;  (*h_filter)[9] = 0;
      (*h_filter)[10] = -1.;(*h_filter)[11] = -2.; (*h_filter)[12] = 17.; (*h_filter)[13] = -2.; (*h_filter)[14] = -1.;
      (*h_filter)[15] = 0; (*h_filter)[16] = -1.; (*h_filter)[17] = -2.; (*h_filter)[18] = -1.; (*h_filter)[19] = 0;
      (*h_filter)[20] = 0;  (*h_filter)[21] = 0;   (*h_filter)[22] = -1.; (*h_filter)[23] = 0;   (*h_filter)[24] = 0;
    }
    break;

    case 2: // Filtro sobel horizonal
    {
        *filterWidth = 3;
        *h_filter = new float[9] {
            -1, 0, 1,
            -2, 0, 2,
            -1, 0, 1
            };
    
    }
    break;

    case 3: // Sobel vertical
    {
        *filterWidth = 3;
        *h_filter = new float[9] {
            -1, -2, -1,
             0, 0, 0,
             1, 2, 1
             };
    }
    break;

    case 4: // CANNY EDGE DETECTOR
    {
        // NO matrix filling is necessary
    }
    break; 

    //TODO: crear los filtros segun necesidad. filter debe contener el filtro al finalizar esta función
    //NOTA: cuidado al establecer el tamaño del filtro a utilizar 

    default:
      printf("Filtro no definido\n");
      exit(1);
  }  
}


// When box_filter is called i the maoin(), three different kernels are executed sequentially
void box_filter(uchar4* const d_inputImageRGBA, 
                uchar4* const d_outputImageRGBA,
                const size_t numRows, const size_t numCols,
                unsigned char* d_redFiltered,
                unsigned char* d_greenFiltered,
                unsigned char* d_blueFiltered,
                int id_filter)
{

  float *h_filter; // Used inside create filter to save it in the GPU (d_filter)
  float *d_filter;  
  int filterWidth;   

  // CANNY_EDGE FILTER
  //En _CANNY_EDGE el metodo tendra que ejcutar todos los pasos llamando a diferentes kernels y creando los filtros en CPU (create_filter) y subiendolos a GPU correspondientes (allocateFilterAndCopyToGPU)
  //En el caso de Box Filter (un único filtro) el metodo realiza la convolucion siguiendo los siguientes pasos 
  #ifdef _CANNY_EDGE
  {
          // Set blocks size
          const dim3 blockSize(TILE_WIDTH, TILE_HEIGHT, 1); 
          const dim3 gridSize((numCols + blockSize.x - 1) / blockSize.x,
              (numRows + blockSize.y - 1) / blockSize.y, 1); // trick to be sure the grid covers the size requested
      
          // Allocating in GPU for g grey and blurred gray image
          checkCudaErrors(cudaMalloc((void**)&d_grey, numRows * numCols * sizeof(unsigned char)));
          checkCudaErrors(cudaMalloc((void**)&d_grey_filtered, numRows * numCols * sizeof(float)));
          checkCudaErrors(cudaMalloc((void**)&d_grey_filtered_char, numRows * numCols * sizeof(unsigned char)));


          // STEP 1 --> Conversion to grey color and blurring
          colorToGrey << <gridSize, blockSize >> > (d_inputImageRGBA,
                                                    d_grey,
                                                    numRows, numCols);
      
          // Creating blur filter
          id_filter = 0; // Changing id_filter to use blur switch
          create_filter(&h_filter, &filterWidth, id_filter);
     
          // Memory for filter
          #ifdef _CONSTANT_MEMORY
                checkCudaErrors(cudaMemcpyToSymbol(constant_filter, h_filter, sizeof(float) * filterWidth * filterWidth));
          #else
                allocateFilterAndCopyToGPU(h_filter, filterWidth, &d_filter);
          #endif

          // Shared memory allocation
          size_t shared_memory = 0;
          #ifdef _SHARED_MEMORY
                  int radius = filterWidth / 2;
                  int dim_SM = TILE_WIDTH + 2 * radius;
                  shared_memory = dim_SM * dim_SM * sizeof(float);
          #endif

          // Convolution with SHARED MEMORY
          convolution << <gridSize, blockSize, shared_memory >> > (d_grey,
                                                                   d_grey_filtered,
                                                                   numRows, numCols,
                                                                   filterWidth,
                                                                   id_filter
                                                                   #ifndef _CONSTANT_MEMORY
                                                                   , d_filter
                                                                   #endif 
                                                                   );
          //checkCudaErrors(cudaDeviceSynchronize());
           
          floatToChar_kernel << <gridSize, blockSize >> > (d_grey_filtered, 
                                                            d_grey_filtered_char,
                                                            numRows, numCols
                                                            );

          //checkCudaErrors(cudaDeviceSynchronize());

          // cleaning first filter????

          // STEP 2 --> Applying Sobel filters
          checkCudaErrors(cudaMalloc(&d_Gx, numRows * numCols * sizeof(float)));
          checkCudaErrors(cudaMalloc(&d_Gy, numRows * numCols * sizeof(float)));
          checkCudaErrors(cudaMalloc(&d_suppression_out, numRows * numCols * sizeof(float))); 
      
          // SOBEL X 
          id_filter = 2; 
          create_filter(& h_filter, &filterWidth, id_filter);

          #ifdef _CONSTANT_MEMORY
                      checkCudaErrors(cudaMemcpyToSymbol(constant_filter, h_filter, sizeof(float) * filterWidth * filterWidth));
          #else
                      allocateFilterAndCopyToGPU(h_filter, filterWidth, &d_filter);
          #endif
     
          size_t shared_memory_Sx = 0;
          #ifdef _SHARED_MEMORY
                radius = filterWidth / 2;
                int dim_Sx = TILE_WIDTH + 2 * radius;
                shared_memory_Sx = dim_Sx * dim_Sx * sizeof(float);
          #endif
          convolution << <gridSize, blockSize, shared_memory_Sx >> > (d_grey_filtered_char,
                                                                      d_Gx, 
                                                                      numRows, numCols, 
                                                                      filterWidth,
                                                                      id_filter
                                                                      #ifndef _CONSTANT_MEMORY
                                                                      , d_filter
                                                                      #endif 
                                                                      );

          //checkCudaErrors(cudaDeviceSynchronize());

          if (h_filter != nullptr) {
              delete[] h_filter;
              h_filter = nullptr; 
          }

          #ifndef _CONSTANT_MEMORY
              if (d_filter != nullptr) {
                  cudaFree(d_filter);
                  d_filter = nullptr;
              }
          #endif

          // SOBEL Y
          id_filter = 3;
          create_filter(& h_filter, &filterWidth, id_filter);

          // Memory for filter
          #ifdef _CONSTANT_MEMORY
                      checkCudaErrors(cudaMemcpyToSymbol(constant_filter, h_filter, sizeof(float)* filterWidth* filterWidth));
          #else
                      allocateFilterAndCopyToGPU(h_filter, filterWidth, &d_filter);
          #endif

          size_t shared_memory_Sy = 0;
          #ifdef _SHARED_MEMORY
                      radius = filterWidth / 2;
                      int dim_Sy = TILE_WIDTH + 2 * radius;
                      shared_memory_Sy = dim_Sy * dim_Sy * sizeof(float);
          #endif
          convolution << <gridSize, blockSize, shared_memory_Sy >> > (d_grey_filtered_char,
                                                                      d_Gy, 
                                                                      numRows, numCols, 
                                                                      filterWidth,
                                                                      id_filter
                                                                      #ifndef _CONSTANT_MEMORY
                                                                      , d_filter
                                                                      #endif 
                                                                      );
          //checkCudaErrors(cudaDeviceSynchronize());

          // IMPORTANT
          // Magnitude computing & pixel suppresion cannot elaborate together
          // The sequential order of operations could lead to RACE CONDITION
      
          // Computing magnitude
          checkCudaErrors(cudaMalloc(&d_mag, numRows* numCols * sizeof(float)));
          magnitude_kernel << <gridSize, blockSize >> > (d_Gx, d_Gy, 
                                                         d_mag, 
                                                         numRows, numCols);
          //checkCudaErrors(cudaDeviceSynchronize());

          // STEP 3 --> Suppressing non_maximum values 
          
          // Looking for maximum value in the input image
          checkCudaErrors(cudaMalloc(&maxPixel_int, sizeof(int)));
          checkCudaErrors(cudaMemset(maxPixel_int, 0, sizeof(int)));

          findMax << <gridSize, blockSize >> > (d_mag, maxPixel_int, 
                                                (int)numRows* numCols
                                                );

          //checkCudaErrors(cudaDeviceSynchronize());

          int maxPixel_cpu = 0;
          checkCudaErrors(cudaMemcpy(&maxPixel_cpu, maxPixel_int, sizeof(int), cudaMemcpyDeviceToHost));
          maxPixel = (float)maxPixel_cpu;

          printf("Max Pixel value: %f\n", maxPixel);

          // Memory allocation
          //checkCudaErrors(cudaMalloc(&d_suppression_out, numRows* numCols * sizeof(float)));

          // TO BYPASS SUPPRESSION to get more evident ouput 
          // UNCOMMENT NEXT LINE--> to convert float to unchar
          //checkCudaErrors(cudaMemcpy(d_suppression_out, d_mag, numRows * numCols * sizeof(float), cudaMemcpyDeviceToDevice));
          // COMMENT SUPPRESSION KERNEL
          
          suppression_kernel << <gridSize, blockSize >> > (d_mag, d_Gx, d_Gy, 
                                                           d_suppression_out, 
                                                           numRows, numCols);
          //checkCudaErrors(cudaDeviceSynchronize());

          // STEP 4 --> Double threshold
          checkCudaErrors(cudaMalloc(&d_threshold_out, numRows* numCols * sizeof(unsigned char)));

          // Defining threshold 
          float highThreshold = 0.09f * maxPixel;
          float lowThreshold = 0.4f * highThreshold;

          threshold_kernel << <gridSize, blockSize >> > (d_suppression_out, d_threshold_out,
                                                         numRows, numCols, 
                                                         lowThreshold, highThreshold);
          //checkCudaErrors(cudaDeviceSynchronize());

          // STEP 5 --> Hysteresis process
          // Output image allocation done in main()
          checkCudaErrors(cudaMalloc(&d_outputImageGrey, numRows* numCols * sizeof(unsigned char)));
          hysteresis_kernel << <gridSize, blockSize >> > (d_threshold_out, d_outputImageGrey,
                                                          numRows, numCols);
          //checkCudaErrors(cudaDeviceSynchronize());

          // Using combine channels to get an output in the main
          recombineChannels << <gridSize, blockSize >> > (d_outputImageGrey,
                                                          d_outputImageGrey,
                                                          d_outputImageGrey,
                                                          d_outputImageRGBA,
                                                          numRows, numCols);

          // CPU WAITS FOR GPU TO END
          checkCudaErrors(cudaDeviceSynchronize());
          // Errors check
          checkCudaErrors(cudaGetLastError());
  }

// BOX FILTER  
#else
      if ( id_filter == 0 || id_filter == 1 ) { // IN case its Blur or Laplacian filter
      
          //TODO: Calcular tamaños de bloque
          const dim3 blockSize(TILE_WIDTH, TILE_HEIGHT, 1); // Set before
          // Formula to activate enough kernels
          const dim3 gridSize((numCols + blockSize.x - 1) / blockSize.x, // dim3 is standrd for CUDA
                              (numRows + blockSize.y - 1) / blockSize.y, 1);

          // Crear el filtro en CPU y subirlo a GPU 
          create_filter(&h_filter, &filterWidth, id_filter);
      
          // Memory for filter
          #ifdef _CONSTANT_MEMORY
                  checkCudaErrors(cudaMemcpyToSymbol(constant_filter, h_filter, sizeof(float) * filterWidth * filterWidth));
          #else
                  allocateFilterAndCopyToGPU(h_filter, filterWidth, &d_filter);
          #endif

          //Crea d_red, d_green y d_blue en GPU. Son variables globales con una vez basta
          allocateMemoryGPU(numRows, numCols);

          //TODO: Lanzar kernel para separar imagenes RGBA en diferentes colores
          separateChannels << <gridSize, blockSize >> > (d_inputImageRGBA,
                                                         numRows,
                                                         numCols,
                                                         d_red,
                                                         d_green,
                                                         d_blue
                                                         );

          checkCudaErrors(cudaGetLastError());

          // CONVOLUTIONS WITH SHARED MEMORY
          //TODO: Ejecutar kernels para convoluciones teniendo uno por canal

          checkCudaErrors(cudaMalloc((void**)&d_red_f, numRows * numCols * sizeof(float)));
          checkCudaErrors(cudaMalloc((void**)&d_green_f, numRows * numCols * sizeof(float)));
          checkCudaErrors(cudaMalloc((void**)&d_blue_f, numRows * numCols * sizeof(float)));
          
          // Shared memory allocation
          size_t shared_memory = 0;

          #ifdef _SHARED_MEMORY
                      int radius = filterWidth / 2;
                      int dim_SM = TILE_WIDTH + 2 * radius;
                      shared_memory = dim_SM * dim_SM * sizeof(float);
          #endif

          // RED
          convolution << <gridSize, blockSize, shared_memory >> > (d_red,
                                                                   d_red_f,
                                                                   numRows, numCols,
                                                                   filterWidth,
                                                                   id_filter
                                                                   #ifndef _CONSTANT_MEMORY
                                                                   , d_filter 
                                                                   #endif
                                                                   );

          // GREEN  
          convolution << <gridSize, blockSize, shared_memory >> > (d_green,
                                                                   d_green_f,
                                                                   numRows, numCols,
                                                                   filterWidth,
                                                                   id_filter
                                                                   #ifndef _CONSTANT_MEMORY
                                                                   , d_filter
                                                                   #endif
                                                                   );

          // BLUE  
          convolution << <gridSize, blockSize, shared_memory >> > (d_blue,
                                                                   d_blue_f,
                                                                   numRows, numCols,
                                                                   filterWidth,
                                                                   id_filter
                                                                   #ifndef _CONSTANT_MEMORY
                                                                   , d_filter
                                                                   #endif
                                                                   );
          
          floatToChar_kernel << <gridSize, blockSize >> > (d_red_f, d_redFiltered, numRows, numCols);
          floatToChar_kernel << <gridSize, blockSize >> > (d_green_f, d_greenFiltered, numRows, numCols);
          floatToChar_kernel << <gridSize, blockSize >> > (d_blue_f, d_blueFiltered, numRows, numCols);
          
          // Recombining the results. 
          recombineChannels << <gridSize, blockSize >> > (d_redFiltered,
                                                          d_greenFiltered,
                                                          d_blueFiltered,
                                                          d_outputImageRGBA,
                                                          numRows,
                                                          numCols);

          // CPU WAITS FOR GPU TO END
          cudaDeviceSynchronize();
          checkCudaErrors(cudaGetLastError());
      }
#endif
  
 // Final cleanings
 // Cleaning filter from gpu (if global memory)
#ifndef _CONSTANT_MEMORY
    checkCudaErrors(cudaFree(d_filter)); // if memory for filter is constant type, no memory was allocated for filters
#endif


// Freeing CPU from filter memory
delete[] h_filter;
}



