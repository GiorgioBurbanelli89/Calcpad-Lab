% CANARIO 1 — constantes builtin dentro de bucles compilados por el JIT.
% Motivo: el JIT trataba pi/e/eps como "variables live-in"; su slot quedaba en 0
% y devolvia CERO sin avisar. En un FEM eso anulaba el angulo de friccion.
% Estos bucles son puro aritmetico (sin disp dentro) para que el JIT los compile:
% si se rinde al interprete el test tambien pasa, pero lo que se vigila es que
% NO devuelva 0 cuando compila.

a=zeros(3,1);
for k=1:3
  a(k)=pi;
end
disp(['CHECK pi ' num2str(a(1),12)]);

b=zeros(3,1);
for k=1:3
  b(k)=22.70*pi/180;
end
disp(['CHECK rad ' num2str(b(1),12)]);

% el caso REAL que rompio el FEM: Drucker-Prager alpha/k de GEO5
al=zeros(2,1); kc=zeros(2,1);
MAT=[22.70 9.0; 38.00 120.0];
for mm=1:2
  ph=atan(tan(MAT(mm,1)*pi/180)/1.0); c=MAT(mm,2); sp=sin(ph); cp=cos(ph);
  al(mm)=2*sp/(sqrt(3)*(3+sp));
  kc(mm)=6*c*cp/(sqrt(3)*(3+sp));
end
disp(['CHECK dp_alpha ' num2str(al(1),12)]);
disp(['CHECK dp_k ' num2str(kc(1),12)]);

% otras constantes que resuelve el evaluador, no el scope.
% OJO: `e` NO se prueba — MATLAB no lo define ('Undefined function or variable')
% y Lab busca compatibilidad con MATLAB, no con Octave.
f=zeros(2,1);
for k=1:2
  f(k)=eps;
end
disp(['CHECK eps ' num2str(f(1),20)]);

g=zeros(2,1);
for k=1:2
  g(k)=1/Inf;
end
disp(['CHECK inv_inf ' num2str(g(1),12)]);
