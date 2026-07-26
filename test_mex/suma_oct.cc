// suma_oct.cc - funcion MEX MVP nativa (C++, extension .cc de Octave) para Hekatan.
// out = A + B (elemento a elemento). Misma ABI hkmex (row-major, memoria del host).
// mkoctfile('suma_oct.cc') compila con g++ (.cc = C++).
extern "C" {

typedef double* (*hkmex_alloc_t)(int index, int rows, int cols);

__declspec(dllexport) void hkmex(
    int nin, const double* const* in, const int* rows, const int* cols,
    int nout, hkmex_alloc_t alloc, int* outRows, int* outCols)
{
    if (nin < 2 || nout < 1) return;
    const double* A = in[0];
    const double* B = in[1];
    int r = rows[0];
    int c = cols[0];
    outRows[0] = r;
    outCols[0] = c;
    double* C = alloc(0, r, c);
    int n = r * c;
    for (int i = 0; i < n; ++i)
        C[i] = A[i] + B[i];
}

} // extern "C"
