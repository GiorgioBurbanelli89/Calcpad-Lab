% mex demo (Fortran): sumar dos matrices en Fortran nativo (gfortran)
mex('suma_f.f90')
A = [1 2; 3 4];
B = [10 20; 30 40];
C = suma_f(A, B)      % debe dar [11 22; 33 44]
