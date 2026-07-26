% mex demo: sumar dos matrices en C++ nativo
mex('suma.cpp')
A = [1 2; 3 4];
B = [10 20; 30 40];
C = suma(A, B)      % debe dar [11 22; 33 44]
