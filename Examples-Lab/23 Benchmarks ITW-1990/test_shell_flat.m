% ============================================================================
%  SANITY del SHELL: placa plana (z=0) como shell -> debe dar 0.443 (= DKQ puro)
% ============================================================================
tic;
E=1e6; nu=0.3; t=0.1; a=10; q=1; nd=6;
D=E*t^3/(12*(1-nu^2)); w_ref=0.00406*q*a^4/D;
N=8; n_j=(N+1)^2; n_e=N*N; h=a/N;
x=zeros(n_j,1); y=zeros(n_j,1);
for j=0:N, for i=0:N, k=j*(N+1)+i+1; x(k)=i*h; y(k)=j*h; end, end
ej=zeros(n_e,4);
for j=0:N-1, for i=0:N-1
    e=j*N+i+1; ej(e,:)=[j*(N+1)+i+1, j*(N+1)+i+2, (j+1)*(N+1)+i+2, (j+1)*(N+1)+i+1];
end, end
ng=nd*n_j; K=zeros(ng,ng);
for e=1:n_e
    q4=ej(e,:); XYZ=[x(q4) y(q4) zeros(4,1)];
    Kg=ke_shell(XYZ,E,nu,t);
    for ic=1:4, for jc=1:4
        i1=nd*(ej(e,ic)-1); i2=nd*(ej(e,jc)-1);
        K(i1+1:i1+6,i2+1:i2+6)=K(i1+1:i1+6,i2+1:i2+6)+Kg(6*(ic-1)+1:6*ic,6*(jc-1)+1:6*jc);
    end, end
end
ks=1e12;
for k=1:n_j
    d=nd*(k-1);
    % fijar GDL en el plano (no cargados): u,v,thz
    K(d+1,d+1)=K(d+1,d+1)+ks; K(d+2,d+2)=K(d+2,d+2)+ks; K(d+6,d+6)=K(d+6,d+6)+ks;
    % SS: w=0 en el borde
    if x(k)==0||x(k)==a||y(k)==0||y(k)==a, K(d+3,d+3)=K(d+3,d+3)+ks; end
end
F=zeros(ng,1); Ae=h*h;
for e=1:n_e, for ic=1:4, kk=ej(e,ic); F(nd*(kk-1)+3)=F(nd*(kk-1)+3)+q*Ae/4; end, end
Z=K\F;
kc=0; for k=1:n_j, if abs(x(k)-a/2)<1e-9 && abs(y(k)-a/2)<1e-9, kc=k; end, end
w_c=Z(nd*(kc-1)+3);
fprintf('SHELL plano 8x8: w_central = %.5f  (ref %.5f, err %+.2f%%)\n', w_c, w_ref, (w_c-w_ref)/w_ref*100);
fprintf('CHECK t_seg %.4f\n', toc);
