! suma_f.f90 - funcion MEX MVP nativa (Fortran) para Hekatan Lab.
! out = A + B (elemento a elemento). MISMA ABI hkmex que C/C++ via iso_c_binding.
!
! El host pasa las matrices ROW-MAJOR como buffers planos. Para una suma elemento
! a elemento el orden es irrelevante (se recorre plano 1..n). Para operaciones que
! dependan de la forma, el usuario debe indexar row-major: elem(i,j) = buf((i-1)*cols + j).
!
! NOTA: la interfaz abstracta del callback se declara LOCAL a la subrutina (no en un
! modulo) porque gfortran 14.2.0 (build de Octave) sufre un ICE al hacer
! c_f_procpointer sobre una interfaz que retorna type(c_ptr) declarada a nivel de modulo.
subroutine hkmex(nin, in, rows, cols, nout, alloc, outRows, outCols) bind(c, name='hkmex')
  use iso_c_binding
  implicit none
  integer(c_int), value :: nin, nout
  type(c_ptr),    value :: in, rows, cols, outRows, outCols
  type(c_funptr), value :: alloc

  ! double* alloc(int index, int rows, int cols)  -- interfaz LOCAL (evita el ICE)
  abstract interface
    function hkmex_alloc_i(idx, nr, nc) bind(c)
      import :: c_ptr, c_int
      integer(c_int), value :: idx, nr, nc
      type(c_ptr) :: hkmex_alloc_i
    end function hkmex_alloc_i
  end interface

  type(c_ptr),     pointer :: inarr(:)
  integer(c_int),  pointer :: rarr(:), carr(:), orarr(:), ocarr(:)
  real(c_double),  pointer :: A(:), B(:), C(:)
  procedure(hkmex_alloc_i), pointer :: allocf
  type(c_ptr) :: cbuf
  integer :: n, i, r, cc

  if (nin < 2 .or. nout < 1) return

  call c_f_pointer(in,      inarr, [nin])
  call c_f_pointer(rows,    rarr,  [nin])
  call c_f_pointer(cols,    carr,  [nin])
  call c_f_pointer(outRows, orarr, [nout])
  call c_f_pointer(outCols, ocarr, [nout])

  r  = rarr(1)
  cc = carr(1)
  n  = r * cc
  call c_f_pointer(inarr(1), A, [n])
  call c_f_pointer(inarr(2), B, [n])

  orarr(1) = r
  ocarr(1) = cc

  call c_f_procpointer(alloc, allocf)
  cbuf = allocf(0_c_int, int(r, c_int), int(cc, c_int))
  call c_f_pointer(cbuf, C, [n])

  do i = 1, n
    C(i) = A(i) + B(i)
  end do
end subroutine hkmex
