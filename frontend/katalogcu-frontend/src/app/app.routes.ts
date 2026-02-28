import { Routes } from '@angular/router';

// Public Components
import { HomeComponent } from './home/home.component';
import { ExploreComponent } from './explore/explore';
import { BlogComponent } from './blog/blog';
import { ServislerComponent } from './servisler/servisler';
import { PricesComponent } from './prices/prices';
import { LoginComponent } from './login/login';
import { RegisterComponent } from './register/register';
import { PublicViewComponent } from './public-view/public-view';
import { PublicCheckoutComponent } from './public-view/public-checkout/public-checkout';
import { PublicCatalogShowcaseComponent } from './public-catalog-showcase/public-catalog-showcase';
import { PublicCatalogViewerComponent } from './public-view/public-catalog-viewer/public-catalog-viewer';

// Admin Components
import { AdminLayoutComponent } from './layout/admin-layout/admin-layout';
import { DashboardComponent } from './dashboard/dashboard';
import { CatalogDetailComponent } from './catalog-detail/catalog-detail';
import { CatalogsComponent } from './dashboard/catalogs/catalogs';
import { PartsComponent } from './dashboard/parts/parts';
import { CustomersComponent } from './dashboard/customers/customers';
import { SettingsComponent } from './dashboard/settings/settings';
import { CatalogAddComponent } from './dashboard/catalogs/catalog-add/catalog-add';
import { PartsAddComponent } from './dashboard/parts/parts-add/parts-add';
import { PartsImportComponent } from './dashboard/parts/parts-import/parts-import';
import { VisualFeedbackComponent } from './dashboard/visual-feedback/visual-feedback';
import { ChatQualityComponent } from './dashboard/chat-quality/chat-quality';
import { OrdersComponent } from './dashboard/orders/orders';
import { planGuard } from './core/guards/plan.guard';
import { planSelectionGuard } from './core/guards/plan-selection.guard';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'public-view/:publicToken', component: PublicViewComponent },
  { path: 'public-view/:publicToken/checkout', component: PublicCheckoutComponent },
  { path: 'view/:id', component: PublicCatalogShowcaseComponent },
  { path: 'view/:id/viewer/:pageIndex', component: PublicCatalogViewerComponent },
  { path: 'explore', component: ExploreComponent },
  { path: 'blog', component: BlogComponent },
  { path: 'services', component: ServislerComponent },
  { path: 'prices', component: PricesComponent },
  { path: 'upgrade', component: PricesComponent },
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  {
    path: 'dashboard',
    component: AdminLayoutComponent,
    canActivate: [planSelectionGuard],
    children: [
      { path: '', component: DashboardComponent },
      { path: 'catalogs', component: CatalogsComponent },
      { path: 'catalog/:id', component: CatalogDetailComponent },
      { path: 'catalogs/new', component: CatalogAddComponent },
      { path: 'customers', component: CustomersComponent, canActivate: [planGuard], data: { minPlan: 3 } },
      { path: 'settings', component: SettingsComponent },
      { path: 'parts', component: PartsComponent, canActivate: [planGuard], data: { minPlan: 3 } },
      { path: 'ecommerce', component: PartsComponent, canActivate: [planGuard], data: { minPlan: 3 } },
      { path: 'parts/new', component: PartsAddComponent },
      { path: 'parts/import', component: PartsImportComponent },
      { path: 'visual-feedback', component: VisualFeedbackComponent, canActivate: [planGuard], data: { minPlan: 2 } },
      { path: 'chat-quality', component: ChatQualityComponent, canActivate: [planGuard], data: { minPlan: 2 } },
      { path: 'ai', component: ChatQualityComponent, canActivate: [planGuard], data: { minPlan: 2 } },
      { path: 'orders', component: OrdersComponent, canActivate: [planGuard], data: { minPlan: 3 } }
    ]
  }
];
