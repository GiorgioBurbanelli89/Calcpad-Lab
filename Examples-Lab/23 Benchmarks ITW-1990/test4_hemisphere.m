% ============================================================================
%  TEST IV - Cascaron hemisferico con hueco de 18 grados (MacNeal-Harder)
%  Shell = membrana ITW + placa DKQ.  Cuarto de modelo, doble simetria.
%  E=68.25e6, nu=0.3, R=10, t=0.04.  Cargas puntuales +-P=1 en el ecuador.
%  Paper Tabla IV (D-type): 4x4=0.0875  8x8=0.0937  12x12=0.0937  16x16=0.0936
%  Referencia (MacNeal-Harder) = 0.094.
% ============================================================================
tic;
E=68.25e6; nu=0.3; R=10; t=0.04; P=1; nd=6;
th0 = 18*pi/180;  th1 = 90*pi/180;      % colatitud: hueco(18) -> ecuador(90)
fprintf('==== TEST IV HEMISFERIO (shell ITW+DKQ) ====  ref = 0.094\n');
for N = [4 8 12 16]
    n_j=(N+1)^2; n_e=N*N;
    x=zeros(n_j,1); y=zeros(n_j,1); z=zeros(n_j,1);
    for i=0:N          % azimut psi 0..90
        for j=0:N      % colatitud
            k=i*(N+1)+j+1;
            psi=(i/N)*(pi/2);  th=th0+(j/N)*(th1-th0);
            x(k)=R*sin(th)*cos(psi); y(k)=R*sin(th)*sin(psi); z(k)=R*cos(th);
        end
    end
    ej=zeros(n_e,4);
    for i=0:N-1, for j=0:N-1
        e=i*N+j+1;
        ej(e,:)=[i*(N+1)+j+1, (i+1)*(N+1)+j+1, (i+1)*(N+1)+j+2, i*(N+1)+j+2];
    end, end
    ng=nd*n_j; K=zeros(ng,ng);
    for e=1:n_e
        q4=ej(e,:); XYZ=[x(q4) y(q4) z(q4)];
        Kg=ke_shell(XYZ,E,nu,t);
        for ic=1:4, for jc=1:4
            i1=nd*(ej(e,ic)-1); i2=nd*(ej(e,jc)-1);
            K(i1+1:i1+6,i2+1:i2+6)=K(i1+1:i1+6,i2+1:i2+6)+Kg(6*(ic-1)+1:6*ic,6*(jc-1)+1:6*jc);
        end, end
    end
    ks=1e12;
    for i=0:N
        for j=0:N
            k=i*(N+1)+j+1; d=nd*(k-1);
            if i==0     % plano x-z (psi=0, y=0): uy=thx=thz=0
                K(d+2,d+2)=K(d+2,d+2)+ks; K(d+4,d+4)=K(d+4,d+4)+ks; K(d+6,d+6)=K(d+6,d+6)+ks;
            end
            if i==N     % plano y-z (psi=90, x=0): ux=thy=thz=0
                K(d+1,d+1)=K(d+1,d+1)+ks; K(d+5,d+5)=K(d+5,d+5)+ks; K(d+6,d+6)=K(d+6,d+6)+ks;
            end
        end
    end
    % quitar traslacion rigida en z: fijar w en el borde del hueco (j=0) de una linea de simetria
    kfix=0*(N+1)+0+1; K(nd*(kfix-1)+3,nd*(kfix-1)+3)=K(nd*(kfix-1)+3,nd*(kfix-1)+3)+ks;
    % cargas: +P en x en el nudo (psi=0, ecuador j=N); -P en y en (psi=90, ecuador j=N)
    F=zeros(ng,1);
    kx=0*(N+1)+N+1;      F(nd*(kx-1)+1)=F(nd*(kx-1)+1)+P;    % +x radial
    ky=N*(N+1)+N+1;      F(nd*(ky-1)+2)=F(nd*(ky-1)+2)-P;    % -y radial
    Z=K\F;
    u_load=abs(Z(nd*(kx-1)+1));      % desplazamiento radial en el punto de carga +x
    fprintf('  %2dx%-2d : u_carga = %.5f\n', N, N, u_load);
end
fprintf('CHECK t_seg %.4f\n', toc);
