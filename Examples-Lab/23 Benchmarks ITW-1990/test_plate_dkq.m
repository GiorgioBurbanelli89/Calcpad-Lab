% ============================================================================
%  VALIDACION DKQ - Placa cuadrada simplemente apoyada, carga uniforme q.
%  Timoshenko (nu=0.3): w_max central = 0.00406 * q a^4 / D ,  D=E t^3/(12(1-v^2))
% ============================================================================
tic;
E = 1e6;  nu = 0.3;  t = 0.1;  a = 10;  q = 1;
D = E*t^3/(12*(1-nu^2));
w_ref = 0.00406 * q * a^4 / D;
for N = [4 8 16]
    n_j = (N+1)^2;  n_e = N*N;  h = a/N;  nd = 3;
    x = zeros(n_j,1); y = zeros(n_j,1);
    for j=0:N, for i=0:N, k=j*(N+1)+i+1; x(k)=i*h; y(k)=j*h; end, end
    e_j = zeros(n_e,4);
    for j=0:N-1, for i=0:N-1
        e=j*N+i+1;
        e_j(e,:)=[j*(N+1)+i+1, j*(N+1)+i+2, (j+1)*(N+1)+i+2, (j+1)*(N+1)+i+1];
    end, end
    ng = nd*n_j;  K = zeros(ng,ng);
    for e=1:n_e
        q4 = e_j(e,:)';
        Ke = ke_dkq(x(q4), y(q4), E, nu, t);
        for ic=1:4, for jc=1:4
            i1=nd*(e_j(e,ic)-1); i2=nd*(e_j(e,jc)-1);
            K(i1+1:i1+3,i2+1:i2+3)=K(i1+1:i1+3,i2+1:i2+3)+Ke(nd*(ic-1)+1:nd*ic,nd*(jc-1)+1:nd*jc);
        end, end
    end
    % SS: w=0 en el borde
    ks=1e12;
    for k=1:n_j
        if x(k)==0||x(k)==a||y(k)==0||y(k)==a
            K(nd*(k-1)+1,nd*(k-1)+1)=K(nd*(k-1)+1,nd*(k-1)+1)+ks;
        end
    end
    % carga uniforme consistente (q*Ae/4 en cada w)
    F=zeros(ng,1); Ae=h*h;
    for e=1:n_e
        for ic=1:4, kk=e_j(e,ic); F(nd*(kk-1)+1)=F(nd*(kk-1)+1)+q*Ae/4; end
    end
    Z=K\F;
    % w central
    kc=0; for k=1:n_j, if abs(x(k)-a/2)<1e-9 && abs(y(k)-a/2)<1e-9, kc=k; end, end
    w_c = Z(nd*(kc-1)+1);
    fprintf('DKQ %2dx%-2d : w_central = %.5f   (ref %.5f, err %+.2f%%)\n', N,N,w_c,w_ref,(w_c-w_ref)/w_ref*100);
end
fprintf('CHECK t_seg %.4f\n', toc);
