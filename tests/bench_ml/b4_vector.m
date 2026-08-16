% #plain
% BENCH 4 - VECTORIZADO grande: elementwise (sin/exp/sqrt), reducciones y sort.
% Mide la fusion de operaciones elementwise y las reducciones sobre 4 millones de
% numeros. Es lo contrario del bench 3: aqui NO hay bucle, todo es matricial.
n = 4000000;
v = (1:n)' * 1e-6;
tmin = Inf;
for rep = 1:3
  t0 = tic;
  a = sin(v) .* exp(-v*1e-3) + sqrt(v);
  s1 = sum(a);
  s2 = max(a);
  s3 = sqrt(sum(a.*a));            % norma 2
  c = cumsum(a);
  s4 = c(n);
  w = sort(a(1:200000), 'descend');
  s5 = w(1) + w(200000);
  tmin = min(tmin, toc(t0));
end
disp(['CHECK b4_suma ' num2str(s1,14)]);
disp(['CHECK b4_max ' num2str(s2,12)]);
disp(['CHECK b4_norma ' num2str(s3,14)]);
disp(['CHECK b4_cumfin ' num2str(s4,14)]);
disp(['CHECK b4_sort ' num2str(s5,12)]);
disp(['CHECK t_seg ' num2str(tmin,6)]);
