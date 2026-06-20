import { Component, OnInit } from '@angular/core';
import { AuthService } from '../../../modules/auth/services/auth.service';

@Component({
    selector: 'app-main-layout',
    templateUrl: './main-layout.component.html',
    styleUrls: []
})
export class MainLayoutComponent implements OnInit {
    username: string = 'User';

    constructor(private authService: AuthService) { }

    ngOnInit(): void {
        this.username = this.authService.getUsername();
    }
}
