import { Component } from '@angular/core';
import { AuthService } from '../../../modules/auth/services/auth.service';

@Component({
    selector: 'app-sidebar',
    templateUrl: './sidebar.component.html',
    styleUrls: []
})
export class SidebarComponent {
    constructor(private authService: AuthService) { }

    logout() {
        this.authService.logout();
    }
}
