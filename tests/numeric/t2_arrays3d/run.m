% CANARIO 2 — arreglos 3-D: zeros(m,n,p), A(i,j,k) lectura y escritura.
% Motivo: la tension de un FEM es 4 x NELEM x NGAUSS. El soporte 3-D se anadio
% en v1.0.75; este test compara SIEMPRE contra el equivalente aplanado, que es
% independiente del camino 3-D. Si difieren, el 3-D se rompio.

tic;
ne=120;
A3=zeros(4,ne,3);
Afl=zeros(4,ne*3);
for e=1:ne
  for q=1:3
    w=[e*0.5; q*1.5; e+q; e*q*0.01];
    A3(:,e,q)=w;
    Afl(:,(e-1)*3+q)=w;
  end
end

s=size(A3);
disp(['CHECK size3d ' num2str(s(1)) ' ' num2str(s(2)) ' ' num2str(s(3))]);

% lectura y comparacion contra el aplanado
dmax=0; acc=0;
for e=1:ne
  for q=1:3
    a=A3(:,e,q); b=Afl(:,(e-1)*3+q);
    v=max(abs(a-b));
    if v>dmax; dmax=v; end
    acc=acc+a(1)+a(2)+a(3)+a(4);
  end
end
disp(['CHECK diff3d_vs_flat ' num2str(dmax,12)]);
disp(['CHECK suma3d ' num2str(acc,12)]);

% forma del slice: MATLAB devuelve columna
v=A3(:,7,2); sv=size(v);
disp(['CHECK shape_slice ' num2str(sv(1)) ' ' num2str(sv(2))]);

% paso a funcion (el FEM pasa sig0 como parametro)
disp(['CHECK diff3d_en_funcion ' num2str(cmp3d(A3,Afl,ne),12)]);
disp(['CHECK t_seg ' num2str(toc,6)]);   % tiempo de computo; se compara vs MATLAB 2017a
