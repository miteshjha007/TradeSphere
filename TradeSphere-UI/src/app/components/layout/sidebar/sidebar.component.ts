import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../../modules/auth/services/auth.service';

@Component({
    selector: 'app-sidebar',
    templateUrl: './sidebar.component.html',
    styleUrls: []
})
export class SidebarComponent {
    isIndianMarketExpanded = true;

    constructor(private authService: AuthService, private router: Router) { }

    isIndianMarketActive(): boolean {
        return this.router.url.startsWith('/indian-market') || this.router.url.startsWith('/ipos');
    }

    toggleIndianMarket(): void {
        this.isIndianMarketExpanded = !this.isIndianMarketExpanded;
    }

    logout() {
        this.authService.logout();
    }
}
