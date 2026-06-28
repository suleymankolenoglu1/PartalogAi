import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule,RouterLink],
  // 👇 DİKKAT: Senin dosya ismin 'header.html' olduğu için burası böyle olmalı
  templateUrl: './header.html', 
  // Eğer CSS dosyanın adı da kısaysa (header.scss) burayı da düzelt:
  styleUrl: './header.css' 
})
export class HeaderComponent {}
