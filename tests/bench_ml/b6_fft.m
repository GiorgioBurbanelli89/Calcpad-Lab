% #plain
% BENCH 6 - FFT (Hekatan Lab la manda a MKL; MATLAB usa FFTW).
% Senal determinista de 2^20 puntos; se verifica con Parseval (la energia en el
% tiempo tiene que ser igual a la energia en la frecuencia dividida por N).
p2 = 20;
n = 2^p2;
k = (0:n-1)';
x = sin(2*pi*17*k/n) + 0.5*cos(2*pi*533*k/n);
tmin = Inf;
for rep = 1:3
  t0 = tic;
  Xf = fft(x);
  Af = abs(Xf);
  s1 = max(Af);
  s2 = sum(Af);
  ener_t = sum(x.*x);
  ener_f = sum(Af.*Af)/n;
  s3 = ener_t;
  s4 = ener_f - ener_t;               % Parseval: debe ser ~0
  y = real(ifft(Xf));
  s5 = max(abs(y-x));                 % ida y vuelta
  tmin = min(tmin, toc(t0));
end
disp(['CHECK b6_pico ' num2str(s1,12)]);
disp(['CHECK b6_summod ' num2str(s2,12)]);
disp(['CHECK b6_energia ' num2str(s3,12)]);
disp(['CHECK b6_parseval_res ' num2str(abs(s4),6)]);
disp(['CHECK b6_ifft_res ' num2str(s5,6)]);
disp(['CHECK t_seg ' num2str(tmin,6)]);
