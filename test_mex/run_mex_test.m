try
  cd('C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\hekatan-lab\test_mex');
  mex suma.cpp
  A = [1 2; 3 4];
  B = [10 20; 30 40];
  C = suma(A, B);
  fid = fopen('matlab_result.txt', 'w');
  fprintf(fid, 'MATLAB R2017a  C = [%g %g; %g %g]\n', C(1,1), C(1,2), C(2,1), C(2,2));
  fclose(fid);
catch e
  fid = fopen('matlab_result.txt', 'w');
  fprintf(fid, 'MATLAB ERROR: %s\n', e.message);
  fclose(fid);
end
exit
