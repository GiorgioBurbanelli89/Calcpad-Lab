% MEX real (mexFunction + mxArray) — el MISMO suma.cpp corre en MATLAB R2017a y en Hekatan
mex('suma.cpp')
A = [1 2; 3 4];
B = [10 20; 30 40];
C = suma(A, B)      % debe dar [11 22; 33 44]
