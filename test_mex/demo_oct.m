% mkoctfile demo (Octave): sumar dos matrices en C++ nativo (.cc)
mkoctfile('suma_oct.cc')
A = [1 2; 3 4];
B = [10 20; 30 40];
C = suma_oct(A, B)      % debe dar [11 22; 33 44]
