% mex demo (C): sumar dos matrices en C nativo (gcc)
mex('suma_c.c')
A = [1 2; 3 4];
B = [10 20; 30 40];
C = suma_c(A, B)      % debe dar [11 22; 33 44]
