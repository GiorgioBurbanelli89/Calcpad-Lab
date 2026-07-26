[X,Y,Z]=ndgrid(1:2, 10:12, 100:100:200);   % Nx=2, Ny=3, Nz=2
xf=X(:); yf=Y(:); zf=Z(:);
disp('size flatten:'); disp(numel(xf));       % debe 12
disp('X(:) ='); disp(xf');
disp('Y(:) ='); disp(yf');
disp('Z(:) ='); disp(zf');
