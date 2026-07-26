/* hekatan mex.h  -- capa MEX/mxArray FIEL a MATLAB (subset) para Hekatan Lab / Octave.
 *
 * Objetivo: que el MISMO .cpp de un MEX real (que usa mexFunction + mxArray) compile y
 * corra sin cambios tanto en MATLAB R2017a como en Hekatan. En MATLAB, #include "mex.h"
 * resuelve al header de MathWorks (extern\include\mex.h); en Hekatan, resuelve a ESTE
 * (via -I<shim>). Las firmas replican las reales de R2017a (matrix.h / mex.h):
 *   void mexFunction(int nlhs, mxArray* plhs[], int nrhs, const mxArray* prhs[]);
 *   size_t mxGetM(const mxArray*);  size_t mxGetN(const mxArray*);
 *   double* mxGetPr(const mxArray*);  void* mxGetData(const mxArray*);
 *   double mxGetScalar(const mxArray*);  bool mxIsDouble(const mxArray*);
 *   mxArray* mxCreateDoubleMatrix(size_t m, size_t n, mxComplexity);
 *
 * MATLAB usa COLUMN-MAJOR: pr[i + j*m] = A(i+1, j+1). El host respeta esa convencion.
 * En MATLAB mxArray es OPACO; aqui es una struct concreta cuyo layout el host C# refleja.
 */
#ifndef HEKATAN_MEX_H
#define HEKATAN_MEX_H

#include <stddef.h>
#include <stdlib.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef size_t mwSize;   /* R2017a (64-bit, large-array API): mwSize = size_t */
typedef size_t mwIndex;

typedef enum { mxREAL = 0, mxCOMPLEX = 1 } mxComplexity;

/* mxClassID: mismo orden que matrix.h de R2017a (DOUBLE = 6). */
typedef enum {
    mxUNKNOWN_CLASS = 0, mxCELL_CLASS, mxSTRUCT_CLASS, mxLOGICAL_CLASS,
    mxCHAR_CLASS, mxVOID_CLASS, mxDOUBLE_CLASS, mxSINGLE_CLASS,
    mxINT8_CLASS, mxUINT8_CLASS, mxINT16_CLASS, mxUINT16_CLASS,
    mxINT32_CLASS, mxUINT32_CLASS, mxINT64_CLASS, mxUINT64_CLASS
} mxClassID;

/* Layout ESPEJADO por el host (C#, [StructLayout(Sequential)]). NO reordenar:
 *   pr(8) pi(8) m(8) n(8) classID(4) complexflag(4) = 40 bytes en Win64. */
typedef struct hk_mxArray {
    double*   pr;           /* datos reales, COLUMN-MAJOR */
    double*   pi;           /* datos imaginarios (NULL si real) */
    size_t    m;            /* filas */
    size_t    n;            /* columnas */
    int       classID;      /* mxClassID */
    int       complexflag;  /* 0 = real, 1 = complejo */
} mxArray;

static mxArray* mxCreateDoubleMatrix(mwSize m, mwSize n, mxComplexity cx)
{
    mxArray* a = (mxArray*)malloc(sizeof(mxArray));
    size_t nel = (size_t)m * (size_t)n;
    a->m = (size_t)m;
    a->n = (size_t)n;
    a->classID = (int)mxDOUBLE_CLASS;
    a->complexflag = (cx == mxCOMPLEX) ? 1 : 0;
    a->pr = (double*)calloc(nel ? nel : 1, sizeof(double));
    a->pi = (cx == mxCOMPLEX) ? (double*)calloc(nel ? nel : 1, sizeof(double)) : NULL;
    return a;
}

static mxArray* mxCreateDoubleScalar(double v)
{
    mxArray* a = mxCreateDoubleMatrix(1, 1, mxREAL);
    a->pr[0] = v;
    return a;
}

static double* mxGetPr(const mxArray* a)        { return a ? a->pr : NULL; }
static double* mxGetPi(const mxArray* a)        { return a ? a->pi : NULL; }
static void*   mxGetData(const mxArray* a)      { return a ? (void*)a->pr : NULL; }
static mwSize  mxGetM(const mxArray* a)         { return a ? (mwSize)a->m : 0; }
static mwSize  mxGetN(const mxArray* a)         { return a ? (mwSize)a->n : 0; }
static size_t  mxGetNumberOfElements(const mxArray* a) { return a ? a->m * a->n : 0; }
static double  mxGetScalar(const mxArray* a)    { return (a && a->pr) ? a->pr[0] : 0.0; }
static int     mxGetClassID(const mxArray* a)   { return a ? a->classID : (int)mxUNKNOWN_CLASS; }
static int     mxIsDouble(const mxArray* a)     { return a && a->classID == (int)mxDOUBLE_CLASS; }
static int     mxIsComplex(const mxArray* a)    { return a && a->complexflag != 0; }
static void    mxDestroyArray(mxArray* a)       { if (a) { free(a->pr); free(a->pi); free(a); } }

/* Forward-declare con enlace C: garantiza que la mexFunction del usuario se exporte
 * como simbolo 'mexFunction' sin mangling (igual que en MATLAB). */
extern void mexFunction(int nlhs, mxArray* plhs[], int nrhs, const mxArray* prhs[]);

#ifdef __cplusplus
}
#endif

#endif /* HEKATAN_MEX_H */
