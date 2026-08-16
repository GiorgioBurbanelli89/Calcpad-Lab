% #plain
% Perfil fino: cada parte se cronometra Y su resultado se USA (para que nadie pueda
% borrarla por "codigo muerto" y regalar un tiempo falso).
n = 4000000;
v = (1:n)' * 1e-6;
r = 3;
t1 = Inf; t2 = Inf; t3 = Inf; t4 = Inf; t5 = Inf; t6 = Inf;
u1 = 0; u2 = 0; u3 = 0; u4 = 0; u5 = 0; u6 = 0;
for rep = 1:r
  t = tic; q = sin(v);        d = toc(t); t1 = min(t1,d); u1 = q(n);
  t = tic; z = exp(v);        d = toc(t); t2 = min(t2,d); u2 = z(n);
  t = tic; y = sqrt(v);       d = toc(t); t3 = min(t3,d); u3 = y(n);
  t = tic; s = sum(v);        d = toc(t); t4 = min(t4,d); u4 = s;
  t = tic; p = v.*v;          d = toc(t); t5 = min(t5,d); u5 = p(n);
  t = tic; g = v*2 + 1;       d = toc(t); t6 = min(t6,d); u6 = g(n);
end
disp(['CHECK f_sin ' num2str(t1,6)]);
disp(['CHECK f_exp ' num2str(t2,6)]);
disp(['CHECK f_sqrt ' num2str(t3,6)]);
disp(['CHECK f_sum ' num2str(t4,6)]);
disp(['CHECK f_mulew ' num2str(t5,6)]);
disp(['CHECK f_axpy ' num2str(t6,6)]);
disp(['CHECK usados ' num2str(u1,10) ' ' num2str(u2,10) ' ' num2str(u3,10) ' ' num2str(u4,14) ' ' num2str(u5,10) ' ' num2str(u6,10)]);
