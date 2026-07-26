/* suma_c.c — funcion MEX MVP nativa (C) para Hekatan Lab.
   out = A + B (elemento a elemento). Misma ABI hkmex que C++ (row-major, memoria del host). */

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
    int n = r * c, i;
    double* C;
    outRows[0] = r;
    outCols[0] = c;
    C = alloc(0, r, c);              /* el host asigna el buffer de salida */
    for (i = 0; i < n; ++i)
        C[i] = A[i] + B[i];
}
