rf = 'C:\Users\j-b-j\AppData\Local\Temp\claude\C--Users-j-b-j-Documents-Hekatan-Calc-1-0-0\df542e56-7f61-4c59-9b9c-18a9da05a420\scratchpad\oracle2.txt';
fid = fopen(rf,'w');
try
  syms x
  y = x; for i=1:5, y=y+i; end
  fprintf(fid, '[1] syms loop: y = %s\n\n', char(y));
  syms Es nus tt
  Dsym = Es*tt^3/(12*(1-nus^2)) * [1, nus, 0; nus, 1, 0; 0, 0, (1-nus)/2];
  fprintf(fid, '[2] disp(Dsym):\n%s\n', evalc('disp(Dsym)'));
catch e
  fprintf(fid, 'ERR: %s\n', e.message);
end
fclose(fid); exit
