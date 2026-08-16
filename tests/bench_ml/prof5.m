% #plain
n = 4000000;
v = (1:n)' * 1e-6;
w = v + 1;
t1 = Inf; t2 = Inf; t3 = Inf; t4 = Inf; t5 = Inf;
u1 = 0; u2 = 0; u3 = 0; u4 = 0; u5 = 0;
for rep = 1:7
  t = tic; a = v.*v;      d = toc(t); t1 = min(t1,d); u1 = a(n);
  t = tic; b = v.*w;      d = toc(t); t2 = min(t2,d); u2 = b(n);
  t = tic; c = v+v;       d = toc(t); t3 = min(t3,d); u3 = c(n);
  t = tic; e1 = v*2;      d = toc(t); t4 = min(t4,d); u4 = e1(n);
  t = tic; f = v.*2;      d = toc(t); t5 = min(t5,d); u5 = f(n);
end
disp(['CHECK g_vv ' num2str(t1,6)]);
disp(['CHECK g_vw ' num2str(t2,6)]);
disp(['CHECK g_suma ' num2str(t3,6)]);
disp(['CHECK g_esc_mtimes ' num2str(t4,6)]);
disp(['CHECK g_esc_ew ' num2str(t5,6)]);
disp(['CHECK usados ' num2str(u1,10) ' ' num2str(u2,10) ' ' num2str(u3,10) ' ' num2str(u4,10) ' ' num2str(u5,10)]);
