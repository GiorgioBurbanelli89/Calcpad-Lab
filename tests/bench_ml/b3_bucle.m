% #plain
% BENCH 3 - BUCLE ESCALAR puro (lo que no se puede vectorizar).
% Es un return-mapping de Newton como el de plasticidad: 1e6 pasos x 5 iteraciones.
% Aqui se compara el JIT de Hekatan Lab contra el JIT de MATLAB; no toca BLAS.
N = 1000000;
tmin = Inf;
for rep = 1:3
  t0 = tic;
  acc = 0;
  for k = 1:N
    x = 1 + mod(k,7)*0.1;
    for it = 1:5
      f = x*x - 2;
      x = x - f/(2*x);      % Newton para sqrt(2)
    end
    acc = acc + x;
  end
  tmin = min(tmin, toc(t0));
end
disp(['CHECK b3_acc ' num2str(acc,14)]);
disp(['CHECK b3_media ' num2str(acc/N,14)]);
disp(['CHECK t_seg ' num2str(tmin,6)]);
