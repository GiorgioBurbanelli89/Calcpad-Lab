syms Es nus tt
Dsym = Es*tt^3/(12*(1-nus^2)) * [1, nus, 0; nus, 1, 0; 0, 0, (1-nus)/2];
rf = 'C:\Users\j-b-j\AppData\Local\Temp\claude\C--Users-j-b-j-Documents-Hekatan-Calc-1-0-0\df542e56-7f61-4c59-9b9c-18a9da05a420\scratchpad\symmat_matlab.txt';
fid = fopen(rf,'w');
try
  s = evalc('disp(Dsym)');
  fprintf(fid, 'MATLAB disp(Dsym):\n%s\n', s);
catch e
  fprintf(fid, 'ERR: %s\n', e.message);
end
fclose(fid);
exit
