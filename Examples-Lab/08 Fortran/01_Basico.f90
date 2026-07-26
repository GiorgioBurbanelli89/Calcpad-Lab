program basico
    ! Hekatan Fortran - lo esencial del lenguaje en un vistazo.
    implicit none

    integer :: i, suma
    real    :: x, y

    ! ---- 1) El loop: el MISMO concepto que en Lab, Py, Octave, C... ----
    suma = 0
    do i = 1, 10
        suma = suma + i
    end do
    print *, 'Suma 1..10        =', suma

    ! ---- 2) Enteros vs reales: el clasico de Fortran ----
    print *, '7/2   con enteros =', 7/2       ! trunca -> 3
    print *, '7.0/2.0 con reales=', 7.0/2.0   ! -> 3.5

    ! ---- 3) Operadores y funciones ----
    x = 3.0
    y = 4.0
    print *, 'x**2 + y**2       =', x**2 + y**2
    print *, 'sqrt(x^2+y^2)     =', sqrt(x**2 + y**2)
    print *, 'max, min, mod     =', max(x,y), min(x,y), mod(7,3)

    ! ---- 4) Condicional ----
    if (y > x) then
        print *, 'y es mayor que x'
    else
        print *, 'x es mayor o igual que y'
    end if

    ! ---- 5) Bucle con paso ----
    print *, 'Pares del 2 al 10:'
    do i = 2, 10, 2
        print *, '   i =', i
    end do

end program basico
