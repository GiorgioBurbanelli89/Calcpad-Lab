% Test simple de loop en hkl CLI
tic
s = 0;
for i = 1:100000
  s = s + i^2;
end
toc
fprintf('Suma final = %.4e\n', s)
fprintf('Esperado   = %.4e (formula N(N+1)(2N+1)/6)\n', 100000*100001*200001/6)
