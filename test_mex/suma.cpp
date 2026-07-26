// suma.cpp - MEX REAL (mexFunction + mxArray), IDENTICO para MATLAB R2017a y Hekatan Lab.
// out = A + B (elemento a elemento). MATLAB usa COLUMN-MAJOR; el bucle plano i=0..m*n-1
// recorre en ese orden (para suma elemento a elemento es equivalente).
#include "mex.h"

void mexFunction(int nlhs, mxArray *plhs[], int nrhs, const mxArray *prhs[])
{
    mwSize m = mxGetM(prhs[0]), n = mxGetN(prhs[0]);
    double *A = mxGetPr(prhs[0]), *B = mxGetPr(prhs[1]);
    plhs[0] = mxCreateDoubleMatrix(m, n, mxREAL);
    double *C = mxGetPr(plhs[0]);
    for (mwSize i = 0; i < m * n; ++i)
        C[i] = A[i] + B[i];
}
